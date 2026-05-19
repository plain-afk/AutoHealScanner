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
using PdfSharp.Pdf;          // KINAKAILANGAN PARA SA PDF GENERATOR
using PdfSharp.Drawing;      // KINAKAILANGAN PARA SA PDF GENERATOR

namespace AutoHealScanner
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> AuditLogs { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<PrinterItem> TargetPrinters { get; set; } = new ObservableCollection<PrinterItem>();

        // BAGONG MEMORY PARA SA BATCH SCANS
        public ObservableCollection<ScannedPage> BatchPages { get; set; } = new ObservableCollection<ScannedPage>();

        private bool _isScanning = false;
        private bool _isHardwareAlertShowing = false;
        private bool _wasPdfViewerVisible = false;
        private string _currentDocumentPath = "";
        private bool _isPrintModalLoaded = false;
        private bool _isAutoConnectEnabled = false;
        private bool _isReconnecting = false;
        private System.Windows.Threading.DispatcherTimer _maintTimer;

        public MainWindow()
        {
            InitializeComponent();
            ListAuditLogs.ItemsSource = AuditLogs;
            ListOutputPrinters.ItemsSource = TargetPrinters;

            // I-bind ang listahan sa Sidebar UI natin
            ListBatchScans.ItemsSource = BatchPages;

            CmbPaperSize.SelectedIndex = 0;
            CmbOrientation.SelectedIndex = 0;
            CmbColorMode.SelectedIndex = 0;
            CmbScaling.SelectedIndex = 0;
            CmbPaperSizeAdvanced.SelectedIndex = 0;
            _isPrintModalLoaded = true;

            LogEvent("System started. Initializing Scanner Module...");
            LoadHardware();
            SetupMaintenanceTimer();
            Task.Run(() => MonitorHardwareStatus());
        }

        // ==========================================
        // BATCH SCANNING & PDF LOGIC
        // ==========================================

        // Pag pinindot mo yung picture sa gilid, lalabas sa gitna
        private void ListBatchScans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListBatchScans.SelectedItem is ScannedPage selected)
            {
                _currentDocumentPath = selected.FilePath;
                ImgPreview.Source = selected.Thumbnail;
                ImgPreview.Visibility = Visibility.Visible;
                PdfViewer.Visibility = Visibility.Collapsed;
                TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnClearBatch_Click(object sender, RoutedEventArgs e)
        {
            BatchPages.Clear();
            _currentDocumentPath = "";
            ImgPreview.Visibility = Visibility.Collapsed;
            PdfViewer.Visibility = Visibility.Collapsed;
            TxtPreviewPlaceholder.Visibility = Visibility.Visible;
            LogEvent("Batch memory cleared. Ready for new scans.");
        }

        private void BtnSavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (BatchPages.Count == 0)
            {
                ShowHardwareAlert("Empty Batch", "Wala pang na-scan na papel. Mag-scan muna bago i-save as PDF.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "PDF Document (*.pdf)|*.pdf";
            sfd.FileName = $"BatchScan_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            sfd.Title = "Save Batch as PDF";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    ShowModal("Compiling PDF Pages...");

                    Task.Run(() => {
                        // Tahi-tahiin natin ang PDF gamit ang PdfSharp
                        PdfSharp.Pdf.PdfDocument document = new PdfSharp.Pdf.PdfDocument();
                        foreach (var page in BatchPages)
                        {
                            PdfPage pdfPage = document.AddPage();
                            using (XGraphics gfx = XGraphics.FromPdfPage(pdfPage))
                            {
                                XImage image = XImage.FromFile(page.FilePath);
                                // Isakto yung laki ng image sa laki ng bond paper
                                pdfPage.Width = image.PixelWidth * 72 / image.HorizontalResolution;
                                pdfPage.Height = image.PixelHeight * 72 / image.VerticalResolution;
                                gfx.DrawImage(image, 0, 0, pdfPage.Width, pdfPage.Height);
                            }
                        }

                        // I-save
                        document.Save(sfd.FileName);

                        Dispatcher.Invoke(() => {
                            HideModal();
                            LogEvent($"BATCH COMPILED: Saved {BatchPages.Count} pages to {sfd.FileName}");
                            ShowHardwareAlert("PDF Saved Successfully", $"Na-save na bilang iisang PDF ang {BatchPages.Count} na pahina mo.");
                        });
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => { HideModal(); ShowHardwareAlert("PDF Error", ex.Message); });
                }
            }
        }

        private void BtnSavePhoto_Click(object sender, RoutedEventArgs e)
        {
            // Check muna kung may na-scan na papel
            if (BatchPages.Count == 0)
            {
                ShowHardwareAlert("Empty Batch", "Wala pang na-scan na papel. Mag-scan muna bago mag-save ng photo.");
                return;
            }

            // Kuhanin ang naka-select na picture sa sidebar. Kung walang naka-select, kuhanin ang unang page.
            ScannedPage pageToSave = ListBatchScans.SelectedItem as ScannedPage;
            if (pageToSave == null) pageToSave = BatchPages[0];

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png";
            sfd.FileName = $"PhotoScan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            sfd.Title = "Save as Photo";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // Dahil may physical file na na-save ang system natin during scan, iko-copy na lang natin iyon.
                    File.Copy(pageToSave.FilePath, sfd.FileName, true);

                    LogEvent($"PHOTO SAVED: Exported {pageToSave.PageLabel} to {Path.GetFileName(sfd.FileName)}");
                    ShowHardwareAlert("Photo Saved Successfully", $"Na-save na ang {pageToSave.PageLabel} bilang litrato sa iyong computer.");
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Save Error", $"Hindi nai-save ang photo: {ex.Message}");
                }
            }
        }

        // ==========================================
        // SMART SCANNER HARDWARE EXECUTION (WIA)
        // ==========================================
        private async Task SimulateProgressAsync()
        {
            int progress = 0;
            while (_isScanning && progress < 90)
            {
                progress += new Random().Next(2, 6);
                if (progress > 90) progress = 90;
                Dispatcher.Invoke(() => { ScanProgressBar.Value = progress; TxtProgressPercent.Text = $"{progress}%"; });
                await Task.Delay(250);
            }
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (CmbActiveHardware.SelectedItem == null) { ShowHardwareAlert("Hardware Error", "Please select an Active Device first."); return; }
            string selectedHardware = CmbActiveHardware.SelectedItem.ToString();

            LogEvent($"Initiating scan command on unified hardware: {selectedHardware}");
            BtnScan.IsEnabled = false; BtnImport.IsEnabled = false; BtnPrint.IsEnabled = false;
            ShowModal("Scanning document. Please wait...");
            _isScanning = true;
            _ = SimulateProgressAsync();
            await Task.Run(() => PerformSilentScan(selectedHardware));
        }

        private void PerformSilentScan(string hardwareName)
        {
            try
            {
                DeviceManager manager = new DeviceManager();
                DeviceInfo targetDeviceInfo = null;
                string cleanPrintName = hardwareName.ToLower().Replace("printer", "").Replace("(copy 1)", "").Replace("(fax)", "").Trim();

                foreach (DeviceInfo info in manager.DeviceInfos)
                {
                    if (info.Type == WiaDeviceType.ScannerDeviceType)
                    {
                        string wiaName = info.Properties["Name"].get_Value()?.ToString() ?? "";
                        string cleanWiaName = wiaName.Split('[')[0].Trim().ToLower();
                        if (cleanWiaName.Contains(cleanPrintName) || cleanPrintName.Contains(cleanWiaName) ||
                            (cleanWiaName.Length > 5 && cleanPrintName.Contains(cleanWiaName.Substring(0, 5))))
                        { targetDeviceInfo = info; break; }
                    }
                }

                if (targetDeviceInfo == null && manager.DeviceInfos.Count > 0)
                {
                    foreach (DeviceInfo info in manager.DeviceInfos)
                        if (info.Type == WiaDeviceType.ScannerDeviceType) { targetDeviceInfo = info; break; }
                }

                if (targetDeviceInfo == null)
                {
                    LogEvent("ERROR: No physical scanner hardware found.");
                    Dispatcher.Invoke(() => ShowHardwareAlert("Scanner Offline", $"Cannot map a physical scanner to [{hardwareName}]. Make sure the cable is connected."));
                    return;
                }

                Device device = targetDeviceInfo.Connect();
                WIA.Item item = device.Items[1];
                LogEvent($"WIA Hardware bridged successfully. Transferring document...");

                string jpegFormat = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
                ImageFile imageFile = (ImageFile)item.Transfer(jpegFormat);

                string saveFolder = @"C:\ScannedDocuments";
                if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);

                string fileName = $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string fullPath = Path.Combine(saveFolder, fileName);

                if (File.Exists(fullPath)) File.Delete(fullPath);
                imageFile.SaveFile(fullPath);
                _isScanning = false;

                Dispatcher.Invoke(() =>
                {
                    ScanProgressBar.Value = 100; TxtProgressPercent.Text = "100%";

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(fullPath); bitmap.EndInit();

                    // IDAGDAG SA GILID NA SIDEBAR ANG BAGONG SCAN
                    ScannedPage newPage = new ScannedPage
                    {
                        FilePath = fullPath,
                        Thumbnail = bitmap,
                        PageLabel = $"Page {BatchPages.Count + 1}"
                    };
                    BatchPages.Add(newPage);
                    ListBatchScans.SelectedItem = newPage; // Auto-select the newly scanned page

                    LogEvent($"SUCCESS: Document saved and added to Batch (Page {BatchPages.Count})");
                });
                Task.Delay(700).Wait();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                _isScanning = false; LogEvent("HARDWARE ERROR: Scanner busy or stuck.");
                Dispatcher.Invoke(() => { TxtModalMessage.Text = "System Jam! Auto-Healing..."; ScanProgressBar.IsIndeterminate = true; TxtProgressPercent.Text = "!"; });
                AutoRestartSpooler();
            }
            catch (Exception ex) { _isScanning = false; LogEvent($"CRITICAL ERROR: {ex.Message}"); }
            finally { _isScanning = false; HideModal(); Dispatcher.Invoke(() => { BtnScan.IsEnabled = true; BtnImport.IsEnabled = true; BtnPrint.IsEnabled = true; }); }
        }

        // ==========================================
        // MAINTENANCE TIMER LOGIC
        // ==========================================
        private void SetupMaintenanceTimer()
        {
            _maintTimer = new System.Windows.Threading.DispatcherTimer();
            _maintTimer.Interval = TimeSpan.FromMinutes(1);
            _maintTimer.Tick += (s, e) => {
                string currentTime = DateTime.Now.ToString("HH:mm");
                if (AdvancedSection.Visibility == Visibility.Visible && currentTime == TxtMaintTime.Text)
                {
                    LogEvent("SCHEDULED MAINTENANCE: Auto-healing system services...");
                    AutoRestartSpooler();
                }
            };
            _maintTimer.Start();
        }

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
                CmbPaperSizeAdvanced.SelectedIndex = 0;
            }
        }

        // ==========================================
        // UNIFIED HARDWARE CONTROL & MONITORING
        // ==========================================
        private void LoadHardware()
        {
            try
            {
                CmbActiveHardware.Items.Clear();
                LocalPrintServer printServer = new LocalPrintServer();
                foreach (PrintQueue pq in printServer.GetPrintQueues())
                    CmbActiveHardware.Items.Add(pq.FullName);
                if (CmbActiveHardware.Items.Count > 0) CmbActiveHardware.SelectedIndex = 0;
            }
            catch { }
        }

        private void BtnRefreshHardware_Click(object sender, RoutedEventArgs e)
        {
            LogEvent("Refreshing connected hardware list...");
            TxtPrinterStatus.Text = "⚪ CHECKING...";
            TxtPrinterStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            LoadHardware();
            LogEvent("Hardware list updated successfully.");
        }

        private void CmbActiveHardware_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TxtPrinterStatus.Text = "⚪ CHECKING...";
            TxtPrinterStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
        }

        private void ToggleAutoConnect_Checked(object sender, RoutedEventArgs e) { _isAutoConnectEnabled = true; LogEvent("Auto-Connect Enabled."); }
        private void ToggleAutoConnect_Unchecked(object sender, RoutedEventArgs e) { _isAutoConnectEnabled = false; LogEvent("Auto-Connect Disabled."); }

        private void BtnForceReconnect_Click(object sender, RoutedEventArgs e)
        {
            string selectedHardware = CmbActiveHardware.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedHardware)) return;
            LogEvent($"Force reconnecting to {selectedHardware}...");
            Task.Run(() => { AutoRestartSpooler(); Dispatcher.Invoke(() => LogEvent($"Reconnection sequence completed.")); });
        }


        // ==========================================
        // ADVANCED PRINT EXECUTION (With N-up Layout)
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
            string paperSizeStr = (CmbPaperSize.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (AdvancedSection.Visibility == Visibility.Visible && CmbPaperSizeAdvanced.SelectedIndex > 0)
                paperSizeStr = (CmbPaperSizeAdvanced.SelectedItem as ComboBoxItem)?.Content.ToString();

            int imagesPerPage = 1;
            if (AdvancedSection.Visibility == Visibility.Visible)
            {
                string layout = (CmbLayout.SelectedItem as ComboBoxItem)?.Content.ToString();
                if (layout.Contains("2-up")) imagesPerPage = 2;
                else if (layout.Contains("4-up")) imagesPerPage = 4;
            }

            LogEvent($"Transmitting to {selectedPrinters.Count} printers. Size: {paperSizeStr}. Layout: {imagesPerPage}-up");
            ShowModal("Sending to selected printers...");

            try
            {
                foreach (string selectedPrinter in selectedPrinters)
                {
                    if (extension == ".pdf")
                    {
                        using (FileStream fs = new FileStream(_currentDocumentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (var document = PdfiumViewer.PdfDocument.Load(fs)) // <-- Nilagyan ng PdfiumViewer
                            {
                                using (var printDocument = document.CreatePrintDocument())
                                {
                                    printDocument.PrinterSettings.PrinterName = selectedPrinter;
                                    printDocument.PrinterSettings.Copies = (short)copies;

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
                        PrintDialog pd = new PrintDialog();
                        pd.PrintQueue = new PrintQueue(new LocalPrintServer(), selectedPrinter);
                        pd.PrintTicket = pd.PrintQueue.DefaultPrintTicket;
                        pd.PrintTicket.CopyCount = copies;

                        if (paperSizeStr.Contains("A4")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                        else if (paperSizeStr.Contains("Legal")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLegal);
                        else if (paperSizeStr.Contains("Folio")) pd.PrintTicket.PageMediaSize = new PageMediaSize(816, 1248);
                        else if (paperSizeStr.Contains("Statement")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaStatement);
                        else if (paperSizeStr.Contains("Executive")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaExecutive);
                        else if (paperSizeStr.Contains("A3")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA3);
                        else pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLetter);

                        string orient = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content.ToString();
                        pd.PrintTicket.PageOrientation = (orient == "Landscape") ? PageOrientation.Landscape : PageOrientation.Portrait;

                        BitmapImage bitmap = new BitmapImage(new Uri(_currentDocumentPath));
                        DrawingVisual visual = new DrawingVisual();
                        using (DrawingContext dc = visual.RenderOpen())
                        {
                            double canvasWidth = pd.PrintableAreaWidth;
                            double canvasHeight = pd.PrintableAreaHeight;

                            if (imagesPerPage == 1)
                            {
                                string scaling = (CmbScaling.SelectedItem as ComboBoxItem)?.Content.ToString();
                                if (scaling == "Fit to Page") dc.DrawImage(bitmap, new Rect(0, 0, canvasWidth, canvasHeight));
                                else dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
                            }
                            else if (imagesPerPage == 2)
                            {
                                double itemHeight = canvasHeight / 2;
                                dc.DrawImage(bitmap, new Rect(0, 0, canvasWidth, itemHeight - 5));
                                dc.DrawImage(bitmap, new Rect(0, itemHeight + 5, canvasWidth, itemHeight - 5));
                            }
                            else if (imagesPerPage == 4)
                            {
                                double itemWidth = canvasWidth / 2;
                                double itemHeight = canvasHeight / 2;
                                dc.DrawImage(bitmap, new Rect(0, 0, itemWidth - 5, itemHeight - 5));
                                dc.DrawImage(bitmap, new Rect(itemWidth + 5, 0, itemWidth - 5, itemHeight - 5));
                                dc.DrawImage(bitmap, new Rect(0, itemHeight + 5, itemWidth - 5, itemHeight - 5));
                                dc.DrawImage(bitmap, new Rect(itemWidth + 5, itemHeight + 5, itemWidth - 5, itemHeight - 5));
                            }
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

            string activeHardware = "";
            if (CmbActiveHardware.SelectedItem != null)
            {
                activeHardware = CmbActiveHardware.SelectedItem.ToString();
            }

            TargetPrinters.Clear();
            LocalPrintServer printServer = new LocalPrintServer();

            foreach (PrintQueue pq in printServer.GetPrintQueues())
            {
                bool isMatched = (pq.FullName == activeHardware);
                TargetPrinters.Add(new PrinterItem { Name = pq.FullName, IsSelected = isMatched });
            }

            bool hasSelection = false;
            foreach (var p in TargetPrinters) { if (p.IsSelected) hasSelection = true; }
            if (!hasSelection && TargetPrinters.Count > 0) TargetPrinters[0].IsSelected = true;

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
                    Dispatcher.Invoke(() => { sel = CmbActiveHardware.SelectedItem?.ToString() ?? ""; });

                    if (!string.IsNullOrEmpty(sel))
                    {
                        PrintQueue q = new PrintQueue(new LocalPrintServer(), sel);
                        q.Refresh();

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
                else
                {
                    ImgPreview.Visibility = Visibility.Visible; PdfViewer.Visibility = Visibility.Collapsed; TxtPreviewPlaceholder.Visibility = Visibility.Collapsed; ImgPreview.Source = new BitmapImage(new Uri(_currentDocumentPath));

                    // Kapag nag-import, isasama na rin natin siya sa Batch Sidebar!
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(_currentDocumentPath); bitmap.EndInit();
                    ScannedPage importedPage = new ScannedPage { FilePath = _currentDocumentPath, Thumbnail = bitmap, PageLabel = $"Page {BatchPages.Count + 1}" };
                    BatchPages.Add(importedPage);
                    ListBatchScans.SelectedItem = importedPage;
                }
                LogEvent("File Imported and added to Batch.");
            }
        }
    }

    // ==========================================
    // MGA DATA CLASSES NATIN SA ILALIM
    // ==========================================
    public class PrinterItem { public string Name { get; set; } public bool IsSelected { get; set; } }

    // BAGONG CLASS PARA SA BATCH SCANS MEMORY
    public class ScannedPage
    {
        public string FilePath { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public string PageLabel { get; set; }
    }
}