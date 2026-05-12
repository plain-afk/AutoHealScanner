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

        // DITO MASE-SAVE ANG MGA CHECKBOXES NATIN
        public ObservableCollection<PrinterItem> TargetPrinters { get; set; } = new ObservableCollection<PrinterItem>();

        private bool _isScanning = false;
        private bool _isHardwareAlertShowing = false;
        private bool _wasPdfViewerVisible = false;
        private string _currentDocumentPath = "";
        private bool _isPrintModalLoaded = false;

        public MainWindow()
        {
            InitializeComponent();
            ListAuditLogs.ItemsSource = AuditLogs;
            ListPrinters.ItemsSource = ScannerList;

            // I-bind ang list ng mga checkboxes sa UI
            ListOutputPrinters.ItemsSource = TargetPrinters;

            CmbPaperSize.SelectedIndex = 0;
            CmbOrientation.SelectedIndex = 0;
            CmbColorMode.SelectedIndex = 0;
            CmbScaling.SelectedIndex = 0;
            _isPrintModalLoaded = true;

            LogEvent("System started. Initializing Asia Integrated Machine Inc. Scanner Module...");
            LoadScanners();
            Task.Run(() => MonitorHardwareStatus());
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentDocumentPath) || !File.Exists(_currentDocumentPath))
            {
                ShowHardwareAlert("Print Error", "Nothing to print. Please scan or import a file first.");
                return;
            }

            // Kuhanin lahat ng Printers sa PC at gawan ng Checkbox bawat isa
            TargetPrinters.Clear();
            LocalPrintServer printServer = new LocalPrintServer();
            foreach (PrintQueue pq in printServer.GetPrintQueues())
            {
                TargetPrinters.Add(new PrinterItem { Name = pq.FullName, IsSelected = false });
            }

            // I-check na agad yung pinaka-unang printer by default
            if (TargetPrinters.Count > 0) TargetPrinters[0].IsSelected = true;

            TxtCopies.Text = "1";

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
                    PaperPreviewBorder.Visibility = Visibility.Visible;
                    ImgPrintPreview.Visibility = Visibility.Collapsed;
                    PdfPrintPreview.Visibility = Visibility.Visible;
                    TxtPdfPreviewMessage.Visibility = Visibility.Visible;

                    PdfPrintPreview.Navigate(new Uri(_currentDocumentPath));
                    PaperPreviewBorder.Width = 260;
                    PaperPreviewBorder.Height = 360;
                    return;
                }

                PaperPreviewBorder.Visibility = Visibility.Visible;
                ImgPrintPreview.Visibility = Visibility.Visible;
                PdfPrintPreview.Visibility = Visibility.Collapsed;
                TxtPdfPreviewMessage.Visibility = Visibility.Collapsed;

                string orientation = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content.ToString();
                string colorMode = (CmbColorMode.SelectedItem as ComboBoxItem)?.Content.ToString();
                string scaling = (CmbScaling.SelectedItem as ComboBoxItem)?.Content.ToString();

                BitmapImage originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(_currentDocumentPath);
                originalImage.EndInit();

                if (colorMode == "Grayscale")
                {
                    FormatConvertedBitmap grayBitmap = new FormatConvertedBitmap();
                    grayBitmap.BeginInit();
                    grayBitmap.Source = originalImage;
                    grayBitmap.DestinationFormat = PixelFormats.Gray8;
                    grayBitmap.EndInit();
                    ImgPrintPreview.Source = grayBitmap;
                }
                else
                {
                    ImgPrintPreview.Source = originalImage;
                }

                if (orientation == "Landscape")
                {
                    PaperPreviewBorder.Width = 360;
                    PaperPreviewBorder.Height = 260;
                    ImgPrintPreview.LayoutTransform = new RotateTransform(90);
                }
                else
                {
                    PaperPreviewBorder.Width = 260;
                    PaperPreviewBorder.Height = 360;
                    ImgPrintPreview.LayoutTransform = new RotateTransform(0);
                }

                if (scaling == "Fit to Page") ImgPrintPreview.Stretch = Stretch.Uniform;
                else ImgPrintPreview.Stretch = Stretch.None;
            }
            catch { }
        }

        private void BtnMinusCopy_Click(object sender, RoutedEventArgs e)
        {
            int currentCopies = int.Parse(TxtCopies.Text);
            if (currentCopies > 1) TxtCopies.Text = (currentCopies - 1).ToString();
        }

        private void BtnPlusCopy_Click(object sender, RoutedEventArgs e)
        {
            int currentCopies = int.Parse(TxtCopies.Text);
            TxtCopies.Text = (currentCopies + 1).ToString();
        }

        private void BtnCancelPrint_Click(object sender, RoutedEventArgs e)
        {
            PdfPrintPreview.Navigate("about:blank");
            PrintModal.Visibility = Visibility.Collapsed;
            if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Visible;
        }

        private async void BtnConfirmPrint_Click(object sender, RoutedEventArgs e)
        {
            // 1. Ipunin lahat ng may Check na Printer
            List<string> selectedPrinters = new List<string>();
            foreach (var printer in TargetPrinters)
            {
                if (printer.IsSelected) selectedPrinters.Add(printer.Name);
            }

            if (selectedPrinters.Count == 0)
            {
                ShowHardwareAlert("Selection Error", "Please check at least one printer to continue.");
                return;
            }

            if (PdfPrintPreview.Visibility == Visibility.Visible)
            {
                PdfPrintPreview.Navigate("about:blank");
                await Task.Delay(500);
            }

            PrintModal.Visibility = Visibility.Collapsed;
            if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Visible;

            int copies = int.Parse(TxtCopies.Text);
            string extension = Path.GetExtension(_currentDocumentPath).ToLower();

            LogEvent($"Preparing BROADCAST print to {selectedPrinters.Count} printers...");
            ShowModal("Sending to selected printers...");
            await Task.Delay(500);

            try
            {
                // =========================================================
                // LOOP: Uulitin ang pag-print sa bawat printer na na-check!
                // =========================================================
                foreach (string selectedPrinter in selectedPrinters)
                {
                    LogEvent($"Transmitting job to {selectedPrinter}...");

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
                                    printDocument.DocumentName = "AutoHealScanner PDF Document";
                                    printDocument.PrintController = new StandardPrintController();
                                    printDocument.Print();
                                }
                            }
                        }
                    }
                    else
                    {
                        System.Windows.Controls.PrintDialog pd = new System.Windows.Controls.PrintDialog();
                        pd.PrintQueue = new PrintQueue(new LocalPrintServer(), selectedPrinter);
                        pd.PrintTicket = pd.PrintQueue.DefaultPrintTicket;
                        pd.PrintTicket.CopyCount = copies;

                        string orientation = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content.ToString();
                        if (orientation == "Landscape") pd.PrintTicket.PageOrientation = PageOrientation.Landscape;
                        else pd.PrintTicket.PageOrientation = PageOrientation.Portrait;

                        string colorMode = (CmbColorMode.SelectedItem as ComboBoxItem)?.Content.ToString();
                        if (colorMode == "Grayscale") pd.PrintTicket.OutputColor = OutputColor.Monochrome;
                        else pd.PrintTicket.OutputColor = OutputColor.Color;

                        string paperSize = (CmbPaperSize.SelectedItem as ComboBoxItem)?.Content.ToString();
                        if (paperSize.Contains("A4")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                        else if (paperSize.Contains("Legal")) pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLegal);
                        else pd.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.NorthAmericaLetter);

                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(_currentDocumentPath);
                        bitmap.EndInit();

                        DrawingVisual visual = new DrawingVisual();
                        using (DrawingContext dc = visual.RenderOpen())
                        {
                            string scaling = (CmbScaling.SelectedItem as ComboBoxItem)?.Content.ToString();

                            if (scaling == "Fit to Page") dc.DrawImage(bitmap, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                            else
                            {
                                double xOffset = (pd.PrintableAreaWidth - bitmap.PixelWidth) / 2;
                                double yOffset = (pd.PrintableAreaHeight - bitmap.PixelHeight) / 2;
                                dc.DrawImage(bitmap, new Rect(xOffset, yOffset, bitmap.PixelWidth, bitmap.PixelHeight));
                            }
                        }
                        pd.PrintVisual(visual, "AutoHealScanner - Image Print");
                    }

                    LogEvent($"SUCCESS: Transmitted to {selectedPrinter}.");
                }

                LogEvent($"BROADCAST COMPLETE: Successfully printed to {selectedPrinters.Count} destinations.");
            }
            catch (Exception ex)
            {
                LogEvent($"PRINT ERROR: {ex.Message}");
            }
            finally
            {
                HideModal();
            }
        }

        private void ShowHardwareAlert(string title, string message)
        {
            if (_isHardwareAlertShowing) return;
            Dispatcher.Invoke(() =>
            {
                _isHardwareAlertShowing = true;
                TxtAlertTitle.Text = title.ToUpper();
                TxtAlertMessage.Text = message;

                _wasPdfViewerVisible = (PdfViewer.Visibility == Visibility.Visible);
                if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Hidden;
                if (PdfPrintPreview.Visibility == Visibility.Visible) PdfPrintPreview.Visibility = Visibility.Hidden;

                AlertModal.Visibility = Visibility.Visible;
            });
        }

        private void BtnCloseAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertModal.Visibility = Visibility.Collapsed;
            _isHardwareAlertShowing = false;
            if (_wasPdfViewerVisible) PdfViewer.Visibility = Visibility.Visible;
            if (PrintModal.Visibility == Visibility.Visible && Path.GetExtension(_currentDocumentPath).ToLower() == ".pdf")
                PdfPrintPreview.Visibility = Visibility.Visible;
        }

        private async Task MonitorHardwareStatus()
        {
            while (true)
            {
                try
                {
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
                    foreach (ManagementObject device in searcher.Get())
                    {
                        ushort errorState = (ushort)(device["DetectedErrorState"] ?? (ushort)0);
                        string deviceName = device["Name"]?.ToString() ?? "Unknown";

                        if (errorState == 8)
                        {
                            LogEvent($"ALERT: Paper Jam detected on {deviceName}");
                            ShowHardwareAlert("Paper Jam Alert", $"Naku! May naipit na papel sa printer: {deviceName}.\n\nPakitanggal muna ang papel bago magpatuloy.");
                        }
                        else if (errorState == 4)
                        {
                            LogEvent($"ALERT: Out of Paper on {deviceName}");
                            ShowHardwareAlert("Out of Paper", $"Ubos na ang papel sa printer: {deviceName}.\n\nPakilagyan ng bagong papel sa tray.");
                        }
                    }
                }
                catch (Exception) { }
                await Task.Delay(2000);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Title = "Select a file to import";
                openFileDialog.Filter = "All Files (*.*)|*.*|PDF Documents (*.pdf)|*.pdf|Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";

                if (openFileDialog.ShowDialog() == true)
                {
                    string sourceFilePath = openFileDialog.FileName;
                    string extension = Path.GetExtension(sourceFilePath).ToLower();
                    string fileName = Path.GetFileName(sourceFilePath);

                    LogEvent($"Importing file: {fileName}...");
                    string saveFolder = @"C:\ScannedDocuments";
                    if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);

                    string destFilePath = Path.Combine(saveFolder, fileName);
                    if (File.Exists(destFilePath)) destFilePath = Path.Combine(saveFolder, $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmss}{extension}");

                    File.Copy(sourceFilePath, destFilePath);
                    _currentDocumentPath = destFilePath;
                    LogEvent($"SUCCESS: File imported to {destFilePath}");

                    if (extension == ".jpg" || extension == ".jpeg" || extension == ".png")
                    {
                        ImgPreview.Visibility = Visibility.Visible;
                        PdfViewer.Visibility = Visibility.Collapsed;
                        TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(destFilePath);
                        bitmap.EndInit();
                        ImgPreview.Source = bitmap;
                    }
                    else if (extension == ".pdf")
                    {
                        PdfViewer.Visibility = Visibility.Visible;
                        ImgPreview.Visibility = Visibility.Collapsed;
                        TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
                        PdfViewer.Navigate(new Uri(destFilePath));
                    }
                    else
                    {
                        PdfViewer.Visibility = Visibility.Collapsed;
                        ImgPreview.Visibility = Visibility.Collapsed;
                        TxtPreviewPlaceholder.Visibility = Visibility.Visible;
                        TxtPreviewPlaceholder.Text = $"[{extension.ToUpper()} FILE IMPORTED]\n\n{Path.GetFileName(destFilePath)}\n\n(Saved securely to C:\\ScannedDocuments)";
                    }
                }
            }
            catch (Exception ex) { LogEvent($"IMPORT ERROR: {ex.Message}"); }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            if (ThemeToggle.IsChecked == true) { theme.SetBaseTheme(Theme.Dark); LogEvent("Switched to Dark Mode."); }
            else { theme.SetBaseTheme(Theme.Light); LogEvent("Switched to Light Mode."); }
            paletteHelper.SetTheme(theme);
        }

        private void LogEvent(string message) { Dispatcher.Invoke(() => { string logEntry = $"[{DateTime.Now:MM/dd/yyyy HH:mm:ss}] {message}"; AuditLogs.Insert(0, logEntry); }); }

        private void ShowModal(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtModalMessage.Text = message;
                ScanProgressBar.Value = 0;
                TxtProgressPercent.Text = "0%";
                if (PdfViewer.Visibility == Visibility.Visible) PdfViewer.Visibility = Visibility.Hidden;
                OverlayModal.Visibility = Visibility.Visible;
            });
        }

        private void HideModal() { Dispatcher.Invoke(() => OverlayModal.Visibility = Visibility.Collapsed); }

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
            if (ListPrinters.SelectedItem == null) { ShowHardwareAlert("Scanner Error", "Please select a scanner from the list first."); return; }
            string? selectedScanner = ListPrinters.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedScanner)) return;

            LogEvent($"Initiating scan on: {selectedScanner}");
            BtnScan.IsEnabled = false; BtnImport.IsEnabled = false; BtnPrint.IsEnabled = false;
            ShowModal("Scanning document. Please wait...");
            _isScanning = true; _ = SimulateProgressAsync();
            await Task.Run(() => PerformSilentScan(selectedScanner));
        }

        private async void PerformSilentScan(string scannerName)
        {
            try
            {
                DeviceManager manager = new DeviceManager();
                DeviceInfo targetDeviceInfo = null;
                foreach (DeviceInfo info in manager.DeviceInfos)
                {
                    if (info.Properties["Name"].get_Value()?.ToString() == scannerName) { targetDeviceInfo = info; break; }
                }

                if (targetDeviceInfo == null) { LogEvent("ERROR: Target scanner went offline."); Dispatcher.Invoke(() => ShowHardwareAlert("Connection Error", "Target scanner went offline. Please check the cable.")); return; }

                Device device = targetDeviceInfo.Connect();
                WIA.Item item = device.Items[1];
                LogEvent("Hardware connected. Transferring document...");

                string jpegFormat = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";
                ImageFile imageFile = (ImageFile)item.Transfer(jpegFormat);

                string saveFolder = @"C:\ScannedDocuments";
                if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);

                string fileName = $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string fullPath = Path.Combine(saveFolder, fileName);

                if (File.Exists(fullPath)) File.Delete(fullPath);
                imageFile.SaveFile(fullPath);
                _currentDocumentPath = fullPath;
                LogEvent($"SUCCESS: Document saved to {fileName}");
                _isScanning = false;

                Dispatcher.Invoke(() =>
                {
                    ScanProgressBar.Value = 100; TxtProgressPercent.Text = "100%";
                    ImgPreview.Visibility = Visibility.Visible; PdfViewer.Visibility = Visibility.Collapsed; TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(fullPath); bitmap.EndInit();
                    ImgPreview.Source = bitmap;
                });
                await Task.Delay(700);
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

        private void LoadScanners()
        {
            try
            {
                ScannerList.Clear();
                DeviceManager manager = new DeviceManager();
                foreach (DeviceInfo info in manager.DeviceInfos)
                {
                    if (info.Type == WiaDeviceType.ScannerDeviceType) ScannerList.Add(info.Properties["Name"].get_Value().ToString());
                }
                if (ScannerList.Count > 0) ListPrinters.SelectedIndex = 0;
            }
            catch (Exception) { LogEvent("SYSTEM ERROR: Failed to load scanner hardware."); }
        }

        private void AutoRestartSpooler()
        {
            try
            {
                ServiceController spooler = new ServiceController("Spooler");
                if (spooler.Status != ServiceControllerStatus.Stopped) { spooler.Stop(); spooler.WaitForStatus(ServiceControllerStatus.Stopped); }
                ClearPrintQueue(); spooler.Start(); spooler.WaitForStatus(ServiceControllerStatus.Running);
                LogEvent("AUTO-HEAL SUCCESS: System ready.");
            }
            catch (Exception) { LogEvent("ERROR: Cannot execute Auto-Heal. Run as Admin."); }
        }

        private void ClearPrintQueue()
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(@"C:\Windows\System32\spool\PRINTERS");
                foreach (FileInfo file in dir.GetFiles()) if (file.Extension.ToLower() == ".shd" || file.Extension.ToLower() == ".spl") file.Delete();
            }
            catch (Exception) { }
        }
    }

    // ==========================================
    // BAGONG CLASS: PARA SA CHECKBOXES NG PRINTER
    // ==========================================
    public class PrinterItem
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }
}