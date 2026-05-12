using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WIA;

namespace AutoHealScanner
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> AuditLogs { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ScannerList { get; set; } = new ObservableCollection<string>();

        private bool _isScanning = false;
        private bool _isHardwareAlertShowing = false;
        private bool _wasPdfViewerVisible = false;

        // BAGONG VARIABLE: Para laging tanda ng system kung anong file ang naka-open!
        private string _currentDocumentPath = "";

        public MainWindow()
        {
            InitializeComponent();
            ListAuditLogs.ItemsSource = AuditLogs;
            ListPrinters.ItemsSource = ScannerList;

            LogEvent("System started. Initializing Asia Integrated Machine Inc. Scanner Module...");
            LoadScanners();
            Task.Run(() => MonitorHardwareStatus());
        }

        // ==========================================
        // FEATURE: MODERN POP-UP ALERT LOGIC
        // ==========================================
        private void ShowHardwareAlert(string title, string message)
        {
            if (_isHardwareAlertShowing) return;

            Dispatcher.Invoke(() =>
            {
                _isHardwareAlertShowing = true;
                TxtAlertTitle.Text = title.ToUpper();
                TxtAlertMessage.Text = message;

                _wasPdfViewerVisible = (PdfViewer.Visibility == Visibility.Visible);
                if (_wasPdfViewerVisible)
                {
                    PdfViewer.Visibility = Visibility.Hidden;
                }

                AlertModal.Visibility = Visibility.Visible;
            });
        }

        private void BtnCloseAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertModal.Visibility = Visibility.Collapsed;
            _isHardwareAlertShowing = false;

            if (_wasPdfViewerVisible)
            {
                PdfViewer.Visibility = Visibility.Visible;
            }
        }

        // ==========================================
        // FEATURE: HARDWARE MONITORING
        // ==========================================
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

        // ==========================================
        // FEATURE: PRINT DOCUMENT (WITH PREVIEW FIX)
        // ==========================================
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentDocumentPath) || !File.Exists(_currentDocumentPath))
                {
                    ShowHardwareAlert("Print Error", "Nothing to print. Please scan or import a file first.");
                    return;
                }

                LogEvent("Opening Windows Native Print Dialog...");

                string extension = Path.GetExtension(_currentDocumentPath).ToLower();

                // Ipapasa natin ang trabaho sa mismong OS para may guaranteed preview window!
                System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo();
                info.FileName = _currentDocumentPath;
                info.Verb = "print";
                info.UseShellExecute = true;

                if (extension == ".pdf")
                {
                    // TANDAAN: Para sa PDF, bubuksan pa rin nito ang Adobe. Kung nakasulat pa rin na 
                    // "This app doesn't support print preview", kailangan mong i-disable ang "New Acrobat" sa settings ng Adobe app mo.
                    info.CreateNoWindow = true;
                    info.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    LogEvent($"SUCCESS: PDF sent to Adobe Print Module.");
                }
                else
                {
                    // Para sa Images, bubuksan nito ang Windows Photo Print Wizard na may malaking preview!
                    LogEvent($"SUCCESS: Image sent to Windows Print Wizard.");
                }

                System.Diagnostics.Process.Start(info);
            }
            catch (Exception ex)
            {
                LogEvent($"PRINT ERROR: {ex.Message}");
            }
        }

        // ==========================================
        // FEATURE: IMPORT FILE
        // ==========================================
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

                    if (File.Exists(destFilePath))
                    {
                        destFilePath = Path.Combine(saveFolder, $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmss}{extension}");
                    }

                    File.Copy(sourceFilePath, destFilePath);

                    // I-SAVE ANG PATH PARA SA PRINTING
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
            catch (Exception ex)
            {
                LogEvent($"IMPORT ERROR: {ex.Message}");
            }
        }

        // ==========================================
        // DARK MODE TOGGLE LOGIC
        // ==========================================
        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            if (ThemeToggle.IsChecked == true)
            {
                theme.SetBaseTheme(Theme.Dark);
                LogEvent("Switched to Dark Mode.");
            }
            else
            {
                theme.SetBaseTheme(Theme.Light);
                LogEvent("Switched to Light Mode.");
            }

            paletteHelper.SetTheme(theme);
        }

        // ==========================================
        // UI HELPERS
        // ==========================================
        private void LogEvent(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string logEntry = $"[{DateTime.Now:MM/dd/yyyy HH:mm:ss}] {message}";
                AuditLogs.Insert(0, logEntry);
            });
        }

        private void ShowModal(string message)
        {
            Dispatcher.Invoke(() =>
            {
                TxtModalMessage.Text = message;
                ScanProgressBar.Value = 0;
                TxtProgressPercent.Text = "0%";

                if (PdfViewer.Visibility == Visibility.Visible)
                {
                    PdfViewer.Visibility = Visibility.Hidden;
                }

                OverlayModal.Visibility = Visibility.Visible;
            });
        }

        private void HideModal()
        {
            Dispatcher.Invoke(() => OverlayModal.Visibility = Visibility.Collapsed);
        }

        private async Task SimulateProgressAsync()
        {
            int progress = 0;
            while (_isScanning && progress < 90)
            {
                progress += new Random().Next(2, 6);
                if (progress > 90) progress = 90;

                Dispatcher.Invoke(() => {
                    ScanProgressBar.Value = progress;
                    TxtProgressPercent.Text = $"{progress}%";
                });

                await Task.Delay(250);
            }
        }

        // ==========================================
        // MAIN SCANNING LOGIC
        // ==========================================
        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinters.SelectedItem == null)
            {
                ShowHardwareAlert("Scanner Error", "Please select a scanner from the list first.");
                return;
            }

            string? selectedScanner = ListPrinters.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedScanner)) return;

            LogEvent($"Initiating scan on: {selectedScanner}");
            BtnScan.IsEnabled = false;
            BtnImport.IsEnabled = false;
            BtnPrint.IsEnabled = false;

            ShowModal("Scanning document. Please wait...");

            _isScanning = true;
            _ = SimulateProgressAsync();

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
                    if (info.Properties["Name"].get_Value()?.ToString() == scannerName)
                    {
                        targetDeviceInfo = info;
                        break;
                    }
                }

                if (targetDeviceInfo == null)
                {
                    LogEvent("ERROR: Target scanner went offline.");
                    Dispatcher.Invoke(() => ShowHardwareAlert("Connection Error", "Target scanner went offline. Please check the cable."));
                    return;
                }

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

                // I-SAVE ANG PATH PARA SA PRINTING
                _currentDocumentPath = fullPath;

                LogEvent($"SUCCESS: Document saved to {fileName}");

                _isScanning = false;

                Dispatcher.Invoke(() =>
                {
                    ScanProgressBar.Value = 100;
                    TxtProgressPercent.Text = "100%";

                    ImgPreview.Visibility = Visibility.Visible;
                    PdfViewer.Visibility = Visibility.Collapsed;
                    TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(fullPath);
                    bitmap.EndInit();
                    ImgPreview.Source = bitmap;
                });

                await Task.Delay(700);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                _isScanning = false;
                LogEvent("HARDWARE ERROR: Scanner busy or stuck.");
                Dispatcher.Invoke(() => {
                    TxtModalMessage.Text = "System Jam! Auto-Healing...";
                    ScanProgressBar.IsIndeterminate = true;
                    TxtProgressPercent.Text = "!";
                });
                AutoRestartSpooler();
            }
            catch (Exception ex)
            {
                _isScanning = false;
                LogEvent($"CRITICAL ERROR: {ex.Message}");
            }
            finally
            {
                _isScanning = false;
                HideModal();
                Dispatcher.Invoke(() => {
                    BtnScan.IsEnabled = true;
                    BtnImport.IsEnabled = true;
                    BtnPrint.IsEnabled = true;
                });
            }
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
                if (spooler.Status != ServiceControllerStatus.Stopped)
                {
                    spooler.Stop();
                    spooler.WaitForStatus(ServiceControllerStatus.Stopped);
                }
                ClearPrintQueue();
                spooler.Start();
                spooler.WaitForStatus(ServiceControllerStatus.Running);
                LogEvent("AUTO-HEAL SUCCESS: System ready.");
            }
            catch (Exception) { LogEvent("ERROR: Cannot execute Auto-Heal. Run as Admin."); }
        }

        private void ClearPrintQueue()
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(@"C:\Windows\System32\spool\PRINTERS");
                foreach (FileInfo file in dir.GetFiles())
                {
                    if (file.Extension.ToLower() == ".shd" || file.Extension.ToLower() == ".spl") file.Delete();
                }
            }
            catch (Exception) { }
        }
    }
}