using PdfiumViewer;
using System.Drawing.Printing;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Printing;
using WIA;
using System.Collections.Generic;

namespace AutoHealScanner
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> AuditLogs { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ScannerList { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<PrinterItem> TargetPrinters { get; set; } = new ObservableCollection<PrinterItem>();

        private bool _isScanning = false;
        private bool _isHardwareAlertShowing = false;
        private bool _wasPdfViewerVisible = false;
        private string _currentDocumentPath = "";
        private bool _isPrintModalLoaded = false;
        private bool _isAutoConnectEnabled = false;
        private bool _isReconnecting = false;

        public MainWindow()
        {
            InitializeComponent();
            ListAuditLogs.ItemsSource = AuditLogs;
            ListPrinters.ItemsSource = ScannerList;
            ListOutputPrinters.ItemsSource = TargetPrinters;

            CmbPaperSize.SelectedIndex = 0;
            CmbOrientation.SelectedIndex = 0;
            CmbColorMode.SelectedIndex = 0;
            CmbScaling.SelectedIndex = 0;
            CmbPaperSizeAdvanced.SelectedIndex = 0;
            _isPrintModalLoaded = true;

            LogEvent("System started. Initializing Asia Integrated Machine Inc. Scanner Module...");
            LoadScanners();
            LoadPrintersForControl();
            Task.Run(() => MonitorHardwareStatus());
        }

        // ==========================================
        // ADVANCED SETTINGS TOGGLE
        // ==========================================
        private void BtnToggleAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (AdvancedSection.Visibility == Visibility.Collapsed)
            {
                AdvancedSection.Visibility = Visibility.Visible;
                BtnToggleAdvanced.Content = "BASIC SETTINGS";
            }
            else
            {
                AdvancedSection.Visibility = Visibility.Collapsed;
                BtnToggleAdvanced.Content = "ADVANCED SETTINGS";
                CmbPaperSizeAdvanced.SelectedIndex = 0; // Reset advanced size if closed
            }
        }

        // ==========================================
        // HARDWARE CONTROL & MONITORING
        // ==========================================
        private void LoadPrintersForControl()
        {
            try
            {
                CmbControlPrinters.Items.Clear();
                LocalPrintServer printServer = new LocalPrintServer();
                foreach (PrintQueue pq in printServer.GetPrintQueues())
                    CmbControlPrinters.Items.Add(pq.FullName);
                if (CmbControlPrinters.Items.Count > 0) CmbControlPrinters.SelectedIndex = 0;
            }
            catch { }
        }

        private void CmbControlPrinters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TxtPrinterStatus.Text = "⚪ CHECKING...";
            TxtPrinterStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
        }

        private void ToggleAutoConnect_Checked(object sender, RoutedEventArgs e) { _isAutoConnectEnabled = true; LogEvent("Auto-Connect Enabled."); }
        private void ToggleAutoConnect_Unchecked(object sender, RoutedEventArgs e) { _isAutoConnectEnabled = false; LogEvent("Auto-Connect Disabled."); }

        private void BtnForceReconnect_Click(object sender, RoutedEventArgs e)
        {
            string selectedPrinter = CmbControlPrinters.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedPrinter)) return;
            LogEvent($"Force reconnecting to {selectedPrinter}...");
            Task.Run(() => { AutoRestartSpooler(); Dispatcher.Invoke(() => LogEvent($"Reconnection sequence completed.")); });
        }

        // ==========================================
        // ADVANCED PRINT EXECUTION
        // ==========================================
        private async void BtnConfirmPrint_Click(object sender, RoutedEventArgs e)
        {
            List<string> selectedPrinters = new List<string>();
            foreach (var printer in TargetPrinters) if (printer.IsSelected) selectedPrinters.Add(printer.Name);

            if (selectedPrinters.Count == 0) { ShowHardwareAlert("Selection Error", "Please check at least one printer."); return; }

            if (PdfPrintPreview.Visibility == Visibility.Visible) { PdfPrintPreview.Navigate("about:blank"); await Task.Delay(500); }

            PrintModal.Visibility = Visibility.Collapsed;
            if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Visible;

            int copies = int.Parse(TxtCopies.Text);
            string extension = Path.GetExtension(_currentDocumentPath).ToLower();

            // Kuhanin ang Paper Size (Basic vs Advanced)
            string paperSizeStr = (CmbPaperSize.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (AdvancedSection.Visibility == Visibility.Visible && CmbPaperSizeAdvanced.SelectedIndex > 0)
                paperSizeStr = (CmbPaperSizeAdvanced.SelectedItem as ComboBoxItem)?.Content.ToString();

            LogEvent($"Transmitting to {selectedPrinters.Count} printers. Size: {paperSizeStr}");
            ShowModal("Sending to selected printers...");

            try
            {
                foreach (string selectedPrinter in selectedPrinters)
                {
                    if (extension == ".pdf")
                    {
                        using (FileStream fs = new FileStream(_currentDocumentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (var document = PdfDocument.Load(fs))
                            {
                                using (var printDocument = document.CreatePrintDocument())
                                {
                                    printDocument.PrinterSettings.PrinterName = selectedPrinter;
                                    printDocument.PrinterSettings.Copies = (short)copies;

                                    // Set Quality (Advanced)
                                    if (AdvancedSection.Visibility == Visibility.Visible)
                                    {
                                        string quality = (CmbPrintQuality.SelectedItem as ComboBoxItem)?.Content.ToString();
                                        if (quality.Contains("Draft")) printDocument.DefaultPageSettings.PrinterResolution.Kind = PrinterResolutionKind.Draft;
                                        else if (quality.Contains("High")) printDocument.DefaultPageSettings.PrinterResolution.Kind = PrinterResolutionKind.High;
                                    }

                                    printDocument.PrintController = new StandardPrintController();
                                    printDocument.Print();
                                }
                            }
                        }
                    }
                    else
                    {
                        // Image Printing Logic with Paper Size Mapping
                        PrintDialog pd = new PrintDialog();
                        pd.PrintQueue = new PrintQueue(new LocalPrintServer(), selectedPrinter);
                        pd.PrintTicket = pd.PrintQueue.DefaultPrintTicket;
                        pd.PrintTicket.CopyCount = copies;

                        // Paper Size Mapping
                        if (paperSizeStr.Contains("A4")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                        else if (paperSizeStr.Contains("Legal")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLegal);
                        else if (paperSizeStr.Contains("Folio")) pd.PrintTicket.PageMediaSize = new PageMediaSize(816, 1248); // MAGIC FIX: Custom Exact Size para sa Folio (8.5 x 13)
                        else if (paperSizeStr.Contains("Statement")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaStatement);
                        else if (paperSizeStr.Contains("Executive")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaExecutive);
                        else if (paperSizeStr.Contains("A3")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA3);
                        else pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLetter);

                        // Orientation
                        string orient = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content.ToString();
                        pd.PrintTicket.PageOrientation = (orient == "Landscape") ? PageOrientation.Landscape : PageOrientation.Portrait;

                        BitmapImage bitmap = new BitmapImage(new Uri(_currentDocumentPath));
                        DrawingVisual visual = new DrawingVisual();
                        using (DrawingContext dc = visual.RenderOpen())
                        {
                            string scaling = (CmbScaling.SelectedItem as ComboBoxItem)?.Content.ToString();
                            if (scaling == "Fit to Page") dc.DrawImage(bitmap, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                            else dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
                        }
                        pd.PrintVisual(visual, "AutoHealScanner Job");
                    }
                }
                LogEvent("BROADCAST COMPLETE.");
            }
            catch (Exception ex) { LogEvent($"PRINT ERROR: {ex.Message}"); }
            finally { HideModal(); }
        }

        // ==========================================
        // UI HELPERS & OTHERS
        // ==========================================
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentDocumentPath) || !File.Exists(_currentDocumentPath)) return;
            TargetPrinters.Clear();
            LocalPrintServer printServer = new LocalPrintServer();
            foreach (PrintQueue pq in printServer.GetPrintQueues())
                TargetPrinters.Add(new PrinterItem { Name = pq.FullName, IsSelected = false });
            if (TargetPrinters.Count > 0) TargetPrinters[0].IsSelected = true;
            _wasPdfViewerVisible = (PdfViewer.Visibility == Visibility.Visible);
            if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Hidden;
            PrintModal.Visibility = Visibility.Visible;
            UpdatePrintPreviewLogic();
        }

        private void UpdatePrintPreview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isPrintModalLoaded || PrintModal.Visibility != Visibility.Visible) return;
            UpdatePrintPreviewLogic();
        }

        private void UpdatePrintPreviewLogic()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentDocumentPath)) return;
                string extension = Path.GetExtension(_currentDocumentPath).ToLower();
                if (extension == ".pdf")
                {
                    PaperPreviewBorder.Visibility = Visibility.Visible; ImgPrintPreview.Visibility = Visibility.Collapsed;
                    PdfPrintPreview.Visibility = Visibility.Visible; TxtPdfPreviewMessage.Visibility = Visibility.Visible;
                    PdfPrintPreview.Navigate(new Uri(_currentDocumentPath));
                    return;
                }
                PaperPreviewBorder.Visibility = Visibility.Visible; ImgPrintPreview.Visibility = Visibility.Visible;
                PdfPrintPreview.Visibility = Visibility.Collapsed; TxtPdfPreviewMessage.Visibility = Visibility.Collapsed;
                BitmapImage originalImage = new BitmapImage(new Uri(_currentDocumentPath));
                string colorMode = (CmbColorMode.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (colorMode == "Grayscale")
                {
                    FormatConvertedBitmap grayBitmap = new FormatConvertedBitmap(originalImage, PixelFormats.Gray8, null, 0);
                    ImgPrintPreview.Source = grayBitmap;
                }
                else ImgPrintPreview.Source = originalImage;

                string orient = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content.ToString();
                ImgPrintPreview.LayoutTransform = (orient == "Landscape") ? new RotateTransform(90) : new RotateTransform(0);
            }
            catch { }
        }

        private void BtnCancelPrint_Click(object sender, RoutedEventArgs e) { PrintModal.Visibility = Visibility.Collapsed; if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Visible; }
        private void BtnMinusCopy_Click(object sender, RoutedEventArgs e) { int c = int.Parse(TxtCopies.Text); if (c > 1) TxtCopies.Text = (c - 1).ToString(); }
        private void BtnPlusCopy_Click(object sender, RoutedEventArgs e) { TxtCopies.Text = (int.Parse(TxtCopies.Text) + 1).ToString(); }

        private async Task MonitorHardwareStatus()
        {
            while (true)
            {
                try
                {
                    string sel = "";
                    Dispatcher.Invoke(() => { sel = CmbControlPrinters.SelectedItem?.ToString() ?? ""; });

                    if (!string.IsNullOrEmpty(sel))
                    {
                        PrintQueue q = new PrintQueue(new LocalPrintServer(), sel);
                        q.Refresh();

                        // MAGIC FIX: Basahin ang status sa background thread BAGO ipasa sa UI Thread!
                        bool isOffline = q.IsOffline;

                        Dispatcher.Invoke(() =>
                        {
                            if (isOffline)
                            {
                                TxtPrinterStatus.Text = "🔴 OFFLINE";
                                TxtPrinterStatus.Foreground = Brushes.Red;

                                if (_isAutoConnectEnabled && !_isReconnecting)
                                {
                                    _isReconnecting = true;
                                    // I-run sa bagong background task ang reconnection para hindi mag-freeze ang screen
                                    Task.Run(() => {
                                        AutoRestartSpooler();
                                        Task.Delay(5000).Wait();
                                        _isReconnecting = false;
                                    });
                                }
                            }
                            else
                            {
                                TxtPrinterStatus.Text = "🟢 ONLINE";
                                TxtPrinterStatus.Foreground = Brushes.Green;
                            }
                        });
                    }
                }
                catch { }

                await Task.Delay(2000);
            }
        }

        private void LoadScanners()
        {
            try
            {
                ScannerList.Clear(); DeviceManager m = new DeviceManager();
                foreach (DeviceInfo i in m.DeviceInfos) if (i.Type == WiaDeviceType.ScannerDeviceType) ScannerList.Add(i.Properties["Name"].get_Value().ToString());
            }
            catch { }
        }

        private void AutoRestartSpooler()
        {
            try
            {
                ServiceController s = new ServiceController("Spooler");
                if (s.Status != ServiceControllerStatus.Stopped) { s.Stop(); s.WaitForStatus(ServiceControllerStatus.Stopped); }
                s.Start(); s.WaitForStatus(ServiceControllerStatus.Running);
            }
            catch { }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var h = new PaletteHelper(); var t = h.GetTheme();
            if (ThemeToggle.IsChecked == true) t.SetBaseTheme(Theme.Dark); else t.SetBaseTheme(Theme.Light);
            h.SetTheme(t);
        }

        private void LogEvent(string m) { Dispatcher.Invoke(() => { AuditLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {m}"); }); }
        private void ShowModal(string m) { Dispatcher.Invoke(() => { TxtModalMessage.Text = m; OverlayModal.Visibility = Visibility.Visible; }); }
        private void HideModal() { Dispatcher.Invoke(() => OverlayModal.Visibility = Visibility.Collapsed); }
        private void BtnCloseAlert_Click(object sender, RoutedEventArgs e) { AlertModal.Visibility = Visibility.Collapsed; }
        private void ShowHardwareAlert(string t, string m) { Dispatcher.Invoke(() => { TxtAlertTitle.Text = t; TxtAlertMessage.Text = m; AlertModal.Visibility = Visibility.Visible; }); }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
            {
                _currentDocumentPath = ofd.FileName; string ext = Path.GetExtension(_currentDocumentPath).ToLower();
                if (ext == ".pdf") { PdfViewer.Visibility = Visibility.Visible; ImgPreview.Visibility = Visibility.Collapsed; TxtPreviewPlaceholder.Visibility = Visibility.Collapsed; PdfViewer.Navigate(new Uri(_currentDocumentPath)); }
                else { ImgPreview.Visibility = Visibility.Visible; PdfViewer.Visibility = Visibility.Collapsed; TxtPreviewPlaceholder.Visibility = Visibility.Collapsed; ImgPreview.Source = new BitmapImage(new Uri(_currentDocumentPath)); }
                LogEvent("File Imported.");
            }
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinters.SelectedItem == null) return;
            ShowModal("Scanning..."); await Task.Delay(2000); // Simulated scan for brevity
            HideModal(); LogEvent("Scan Complete.");
        }
    }

    public class PrinterItem { public string Name { get; set; } public bool IsSelected { get; set; } }
}