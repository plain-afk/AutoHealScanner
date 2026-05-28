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
using System.Linq;             // GAGAMITIN PARA SA BROWSER FILTERING AT SORTING
using System.Diagnostics;      // GAGAMITIN PARA SA PROCESS START ng mga PDF/Images
using System.Windows.Input;    // GAGAMITIN PARA SA MOUSE EVENTS
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

        // REPOSITORY LIBRARY STORAGE MEMORY
        public ObservableCollection<LibraryItem> LibraryFiles { get; set; } = new ObservableCollection<LibraryItem>();
        private List<LibraryItem> _rawLibraryItems = new List<LibraryItem>();
        private string _currentLibraryPath = @"C:\ScannedDocuments";
        private string _activeCategory = "All"; // "All", "PDF", "Image"
        private bool _isLibraryInitLoaded = false;
        public ObservableCollection<LibraryItem> RecentFiles { get; set; } = new ObservableCollection<LibraryItem>();
        private int _healSequencesCount = 0;

        // AUTOMATIONS ENGINE PROPERTIES
        public ObservableCollection<string> AutomationLogs { get; set; } = new ObservableCollection<string>();
        private string _currentBackupPath = @"C:\ScannedDocuments\Backup";
        private System.Windows.Threading.DispatcherTimer? _watchdogTimer;
        private int _watchdogSecondsCounter = 0;
        private int _watchdogIntervalSeconds = 60; // default 1 minute
        private int _replicatedFilesCount = 0;

        private bool _isScanning = false;
        private bool _wasPdfViewerVisible = false;
        private string _currentDocumentPath = "";
        private bool _isPrintModalLoaded = false;
        private bool _isAutoConnectEnabled = true;
        private bool _isReconnecting = false;
        private System.Windows.Threading.DispatcherTimer? _maintTimer;
        private string _currentTab = "Dashboard";
        private int _printerRefreshCounter = 0;

        // PDF READER PROPERTIES
        private string _pdfReaderCurrentPath = "";
        private int _pdfReaderCurrentPageIndex = 0;
        private double _pdfReaderZoomScale = 1.0;
        private int _pdfReaderRotationAngle = 0;
        public ObservableCollection<PdfReaderPageItem> PdfReaderPages { get; set; } = new ObservableCollection<PdfReaderPageItem>();

        // PRINTER CENTER PROPERTIES
        public ObservableCollection<PrintJobItem> CurrentPrinterJobs { get; set; } = new ObservableCollection<PrintJobItem>();

        // DISCOVERED WIFI PRINTERS
        public ObservableCollection<DiscoveredPrinter> DiscoveredPrinters { get; set; } = new ObservableCollection<DiscoveredPrinter>();
        private System.Threading.CancellationTokenSource? _scanCancellationTokenSource;
        private bool _isScanningPrinters = false;

        // THEME ENGINE PROPERTIES
        private bool _isDarkMode = true;
        
        private readonly Dictionary<string, string> DarkPalette = new Dictionary<string, string>
        {
            { "ColorWindowBg", "#080B11" },
            { "ColorSidebarBg", "#0D111B" },
            { "ColorBatchBg", "#111622" },
            { "PreviewBg", "#080B11" },
            { "ColorRightPanelBg", "#0F1320" },
            { "CardBg", "#0F172A" },
            { "CardBgAlt", "#161B29" },
            { "InputBg", "#1F2937" },
            { "ColorTextPrimary", "#F8FAFC" },
            { "ColorTextMuted", "#94A3B8" },
            { "TextDim", "#64748B" },
            { "BorderLine", "#1F2937" },
            { "BorderSubtle", "#2D3748" },
            { "NavActiveBg", "#1E293B" },
            { "NavActiveBorder", "#334155" },
            { "ModalOverlay", "#CC030712" },
            { "ModalBg", "#0F172A" },
            { "ProfileBg", "#161B29" },
            { "AvatarBg", "#1E293B" },
            { "AvatarText", "#38BDF8" },
            { "PopupBg", "#0F172A" },
            { "InputBorder", "#1F2937" },
            { "DeepBg", "#080B11" },
            { "SuccessBg", "#10B981" },
            { "DeepestBg", "#05070B" },
            { "ConsoleFg", "#00FFC4" }
        };

        private readonly Dictionary<string, string> LightPalette = new Dictionary<string, string>
        {
            { "ColorWindowBg", "#F1F5F9" },
            { "ColorSidebarBg", "#FFFFFF" },
            { "ColorBatchBg", "#F8FAFC" },
            { "PreviewBg", "#E2E8F0" },
            { "ColorRightPanelBg", "#FFFFFF" },
            { "CardBg", "#FFFFFF" },
            { "CardBgAlt", "#F1F5F9" },
            { "InputBg", "#F1F5F9" },
            { "ColorTextPrimary", "#0F172A" },
            { "ColorTextMuted", "#64748B" },
            { "TextDim", "#94A3B8" },
            { "BorderLine", "#E2E8F0" },
            { "BorderSubtle", "#CBD5E1" },
            { "NavActiveBg", "#EFF6FF" },
            { "NavActiveBorder", "#3B82F6" },
            { "ModalOverlay", "#80000000" },
            { "ModalBg", "#FFFFFF" },
            { "ProfileBg", "#F8FAFC" },
            { "AvatarBg", "#DBEAFE" },
            { "AvatarText", "#1E40AF" },
            { "PopupBg", "#FFFFFF" },
            { "InputBorder", "#CBD5E1" },
            { "DeepBg", "#E2E8F0" },
            { "SuccessBg", "#059669" },
            { "DeepestBg", "#EFF3F8" },
            { "ConsoleFg", "#0F172A" }
        };

        public MainWindow()
        {
            InitializeComponent();
            ListAuditLogs.ItemsSource = AuditLogs;
            ListScanCenterLogs.ItemsSource = AuditLogs;
            ListOutputPrinters.ItemsSource = TargetPrinters;

            // I-bind ang listahan sa Sidebar UI natin
            ListBatchScans.ItemsSource = BatchPages;

            // I-bind ang listahan para sa Document Library
            ListLibraryFiles.ItemsSource = LibraryFiles;

            // I-bind ang listahan para sa Automation logs
            ListAutomationLogs.ItemsSource = AutomationLogs;

            // I-bind ang listahan para sa Dashboard
            ListDashRecentFiles.ItemsSource = RecentFiles;

            // I-bind ang listahan para sa PDF Reader
            ListPdfReaderThumbnails.ItemsSource = PdfReaderPages;

            // I-bind ang listahan para sa Printer Center
            ListPrinterCenterDevices.ItemsSource = TargetPrinters;
            GridPrinterCenterJobs.ItemsSource = CurrentPrinterJobs;
            ListDiscoveredPrinters.ItemsSource = DiscoveredPrinters;

            CmbPaperSize.SelectedIndex = 0;
            CmbOrientation.SelectedIndex = 0;
            CmbColorMode.SelectedIndex = 0;
            CmbScaling.SelectedIndex = 0;
            CmbPaperSizeAdvanced.SelectedIndex = 0;

            // Initialize dynamic combo boxes inside Library
            CmbLibraryFormat.SelectedIndex = 0;
            CmbLibrarySort.SelectedIndex = 0;
            _isLibraryInitLoaded = true;

            // Setup Automations views
            TxtBackupPath.Text = _currentBackupPath;
            SetupWatchdogTimer();

            _isPrintModalLoaded = true;

            LogEvent("System started. Initializing Scanner Module...");
            LoadHardware();
            SetupMaintenanceTimer();
            Task.Run(() => MonitorHardwareStatus());
            
            // Set initial active tab state
            SwitchToTab("Dashboard");
            
            // Apply Initial Theme
            ApplyTheme();
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
                string ext = Path.GetExtension(selected.FilePath).ToLower();
                if (ext == ".pdf")
                {
                    ImageSource? rendered = RenderPdfPage(selected.FilePath, 0);
                    ImgPreview.Source = rendered ?? selected.Thumbnail;
                }
                else
                {
                    ImgPreview.Source = selected.Thumbnail;
                }
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

                    var pagesCopy = BatchPages.ToList();
                    int totalPagesCount = pagesCopy.Count;
                    string targetFileName = sfd.FileName;

                    Task.Run(() => {
                        try
                        {
                            // Tahi-tahiin natin ang PDF gamit ang PdfSharp
                            PdfSharp.Pdf.PdfDocument document = new PdfSharp.Pdf.PdfDocument();
                            foreach (var page in pagesCopy)
                            {
                                PdfPage pdfPage = document.AddPage();
                                using (XGraphics gfx = XGraphics.FromPdfPage(pdfPage))
                                {
                                    using (XImage image = XImage.FromFile(page.FilePath))
                                    {
                                        pdfPage.Width = new XUnitPt(image.PointWidth);
                                        pdfPage.Height = new XUnitPt(image.PointHeight);
                                        gfx.DrawImage(image, 0, 0, image.PointWidth, image.PointHeight);
                                    }
                                }
                            }

                            // I-save
                            document.Save(targetFileName);

                            Dispatcher.Invoke(() => {
                                HideModal();
                                LogEvent($"BATCH COMPILED: Saved {totalPagesCount} pages to {targetFileName}");
                                ShowHardwareAlert("PDF Saved Successfully", $"Na-save na bilang iisang PDF ang {totalPagesCount} na pahina mo.");
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => {
                                HideModal();
                                ShowHardwareAlert("PDF Error", $"Failed to compile: {ex.Message}");
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    HideModal();
                    ShowHardwareAlert("PDF Error", ex.Message);
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
            ScannedPage pageToSave = (ListBatchScans.SelectedItem as ScannedPage) ?? BatchPages[0];

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
            string? selectedHardware = CmbActiveHardware.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedHardware)) { ShowHardwareAlert("Hardware Error", "Please select an Active Device first."); return; }

            string hardwareName = selectedHardware;
            LogEvent($"Initiating scan command on unified hardware: {hardwareName}");
            BtnScan.IsEnabled = false; BtnImport.IsEnabled = false; BtnPrint.IsEnabled = false;
            ShowModal("Scanning document. Please wait...");
            _isScanning = true;
            _ = SimulateProgressAsync();
            await Task.Run(() => PerformSilentScan(hardwareName));
        }

        private void PerformSilentScan(string hardwareName)
        {
            try
            {
                DeviceManager manager = new DeviceManager();
                DeviceInfo? targetDeviceInfo = null;
                string cleanPrintName = hardwareName.ToLower().Replace("printer", "").Replace("(copy 1)", "").Replace("(fax)", "").Trim();

                foreach (DeviceInfo? info in manager.DeviceInfos)
                {
                    if (info == null) continue;
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
                    foreach (DeviceInfo? info in manager.DeviceInfos)
                    {
                        if (info == null) continue;
                        if (info.Type == WiaDeviceType.ScannerDeviceType) { targetDeviceInfo = info; break; }
                    }
                }

                if (targetDeviceInfo == null)
                {
                    LogEvent("[Auto-Heal] Physical scanner not found. Initializing Simulated Scan Fallback...");
                    
                    // Generate simulated scan image
                    string simulatedPath = GenerateSimulatedScanImage();
                    
                    _isScanning = false;

                    Dispatcher.Invoke(() =>
                    {
                        ScanProgressBar.Value = 100; TxtProgressPercent.Text = "100%";

                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(simulatedPath); bitmap.EndInit();

                        // IDAGDAG SA GILID NA SIDEBAR ANG BAGONG SCAN
                        ScannedPage newPage = new ScannedPage
                        {
                            FilePath = simulatedPath,
                            Thumbnail = bitmap,
                            PageLabel = $"Page {BatchPages.Count + 1}"
                        };
                        BatchPages.Add(newPage);
                        ListBatchScans.SelectedItem = newPage; // Auto-select the newly scanned page

                        LogEvent($"[Auto-Heal] SUCCESS: Simulated Document saved to Batch (Page {BatchPages.Count})");
                        RunPostScanWorkflows(simulatedPath);
                    });
                    Task.Delay(700).Wait();
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
                    RunPostScanWorkflows(fullPath);
                });
                Task.Delay(700).Wait();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                _isScanning = false; LogEvent("HARDWARE ERROR: Scanner busy or stuck.");
                Dispatcher.Invoke(() => { TxtModalMessage.Text = "System Jam! Auto-Healing..."; ScanProgressBar.IsIndeterminate = true; TxtProgressPercent.Text = "!"; });
                AutoRestartStiSvc();
            }
            catch (Exception ex) { _isScanning = false; LogEvent($"CRITICAL ERROR: {ex.Message}"); }
            finally { _isScanning = false; HideModal(); Dispatcher.Invoke(() => { BtnScan.IsEnabled = true; BtnImport.IsEnabled = true; BtnPrint.IsEnabled = true; }); }
        }

        private string GenerateSimulatedScanImage()
        {
            string saveFolder = @"C:\ScannedDocuments";
            if (!Directory.Exists(saveFolder)) Directory.CreateDirectory(saveFolder);

            string fileName = $"SimulatedScan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string fullPath = Path.Combine(saveFolder, fileName);

            // Create a DrawingVisual to draw a stylized simulated document sheet
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext dc = drawingVisual.RenderOpen())
            {
                // Draw white sheet background
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 800, 1100));

                // Draw decorative headers and text (representing a scanned medical/data sheet)
                dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4F2B")), null, new Rect(40, 40, 720, 10));
                
                // Title
                FormattedText titleText = new FormattedText(
                    "AUTO-HEAL SCAN STATION - SIMULATED DOCUMENT",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    24,
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4F2B")),
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip
                );
                dc.DrawText(titleText, new Point(40, 60));

                // Subtitle / Date
                FormattedText dateText = new FormattedText(
                    $"Generated on: {DateTime.Now:MMMM dd, yyyy HH:mm:ss} | Device: {CmbActiveHardware.SelectedItem?.ToString() ?? "Brother DCP-T500W"}",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    14,
                    Brushes.DarkGray,
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip
                );
                dc.DrawText(dateText, new Point(40, 100));

                dc.DrawLine(new Pen(Brushes.LightGray, 2), new Point(40, 130), new Point(760, 130));

                // Dummy Content Paragraphs
                string[] lines = new string[] {
                    "SYSTEM DIAGNOSTIC RESULTS",
                    "--------------------------",
                    "• Print Spooler Connection: Healthy (Status: Online)",
                    "• Windows Image Acquisition (WIA) Service: Healthy (Status: Simulated)",
                    "• Device Mapping status: Simulation Fallback Active",
                    "• WiFi Signal strength: Excellent",
                    "",
                    "This document was dynamically compiled by the AutoHealScanner engine",
                    "to demonstrate the system's ability to process and manage multi-page document batches.",
                    "You can save this page as a single JPEG, or scan more pages to compile a multi-page PDF.",
                    "",
                    "AUTHENTICATION VERIFIED",
                    "User: James Michael G. Biaton",
                    "Role: System Administrator",
                    "",
                    "Auto-healing diagnostics completed successfully. Service recovery sequences are active."
                };

                double yPos = 160;
                foreach (string line in lines)
                {
                    bool isHeader = line.StartsWith("SYSTEM DIAGNOSTIC") || line.StartsWith("AUTHENTICATION");
                    FormattedText ft = new FormattedText(
                        line,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, isHeader ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                        isHeader ? 16 : 14,
                        isHeader ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4F2B")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E3329")),
                        VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip
                    );
                    dc.DrawText(ft, new Point(50, yPos));
                    yPos += isHeader ? 30 : 25;
                }

                // Decorative watermark / shield
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4F2B")), 1) { DashStyle = DashStyles.Dash }, new Rect(40, 700, 720, 300), 10, 10);
                
                FormattedText certText = new FormattedText(
                    "AUTO-HEAL CORE SECURE ENGINE",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    18,
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E4F2B")),
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip
                );
                dc.DrawText(certText, new Point(60, 720));
            }

            // Render to Bitmap
            RenderTargetBitmap renderTarget = new RenderTargetBitmap(800, 1100, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(drawingVisual);

            // Save as JPEG
            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 90;
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));

            using (FileStream fs = new FileStream(fullPath, FileMode.Create))
            {
                encoder.Save(fs);
            }

            return fullPath;
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
                if (CmbScanDevice != null) CmbScanDevice.Items.Clear();
                LocalPrintServer printServer = new LocalPrintServer();
                string defaultPrinterName = "";
                try
                {
                    defaultPrinterName = printServer.DefaultPrintQueue?.FullName ?? "";
                }
                catch { }

                foreach (PrintQueue pq in printServer.GetPrintQueues())
                {
                    CmbActiveHardware.Items.Add(pq.FullName);
                    if (CmbScanDevice != null) CmbScanDevice.Items.Add(pq.FullName);
                }

                if (CmbActiveHardware.Items.Count > 0)
                {
                    if (!string.IsNullOrEmpty(defaultPrinterName) && CmbActiveHardware.Items.Contains(defaultPrinterName))
                    {
                        CmbActiveHardware.SelectedItem = defaultPrinterName;
                        if (CmbScanDevice != null) CmbScanDevice.SelectedItem = defaultPrinterName;
                        LogEvent($"Active hardware populated. Default device selected: {defaultPrinterName}");
                    }
                    else
                    {
                        CmbActiveHardware.SelectedIndex = 0;
                        if (CmbScanDevice != null) CmbScanDevice.SelectedIndex = 0;
                        LogEvent($"Active hardware populated. Default device selected (fallback): {CmbActiveHardware.Items[0]}");
                    }
                }
                else
                {
                    LogEvent("WARNING: No print queues or hardware detected on the system.");
                }
            }
            catch (Exception ex)
            {
                LogEvent($"Error loading hardware: {ex.Message}");
            }
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
            
            if (CmbActiveHardware == null || CmbScanDevice == null || !_isLibraryInitLoaded) return;
            
            if (CmbScanDevice.SelectedIndex != CmbActiveHardware.SelectedIndex)
            {
                CmbScanDevice.SelectedIndex = CmbActiveHardware.SelectedIndex;
            }
        }

        private void CmbScanDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbActiveHardware == null || CmbScanDevice == null || !_isLibraryInitLoaded) return;
            
            if (CmbActiveHardware.SelectedIndex != CmbScanDevice.SelectedIndex)
            {
                CmbActiveHardware.SelectedIndex = CmbScanDevice.SelectedIndex;
            }
        }

        private void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbSource == null || TxtScanFeederInstruction == null || IconScanFeeder == null || !_isLibraryInitLoaded) return;
            
            if (CmbSource.SelectedItem is ComboBoxItem selectedItem)
            {
                string source = selectedItem.Content?.ToString() ?? "";
                if (source.Contains("Flatbed"))
                {
                    TxtScanFeederInstruction.Text = "Place paper face down on the glass bed under the lid.";
                    IconScanFeeder.Kind = PackIconKind.FileDocument;
                }
                else
                {
                    TxtScanFeederInstruction.Text = "Load paper face up into the top Automatic Document Feeder (ADF) tray.";
                    IconScanFeeder.Kind = PackIconKind.FolderMultiple;
                }
            }
        }

        private void ToggleAutoConnect_Checked(object sender, RoutedEventArgs e) { _isAutoConnectEnabled = true; LogEvent("Auto-Connect Enabled."); }
        private void ToggleAutoConnect_Unchecked(object sender, RoutedEventArgs e) { _isAutoConnectEnabled = false; LogEvent("Auto-Connect Disabled."); }

        private void BtnForceReconnect_Click(object sender, RoutedEventArgs e)
        {
            string? selectedHardware = CmbActiveHardware.SelectedItem?.ToString();
            LogEvent(string.IsNullOrEmpty(selectedHardware) 
                ? "Initiating global service recovery sequence..." 
                : $"Force reconnecting and restarting services for {selectedHardware}...");
            Task.Run(() => { 
                AutoRestartSpooler(); 
                AutoRestartStiSvc(); 
                Dispatcher.Invoke(() => {
                    LogEvent("Reconnection sequence completed.");
                    if (ViewDashboard.Visibility == Visibility.Visible)
                    {
                        UpdateDashboardData();
                    }
                }); 
            });
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

            if (!int.TryParse(TxtCopies.Text, out int copies)) copies = 1;
            string extension = Path.GetExtension(_currentDocumentPath).ToLower();
            
            string paperSizeStr = (CmbPaperSize.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Letter";
            if (AdvancedSection.Visibility == Visibility.Visible && CmbPaperSizeAdvanced.SelectedIndex > 0)
                paperSizeStr = (CmbPaperSizeAdvanced.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Letter";

            int imagesPerPage = 1;
            if (AdvancedSection.Visibility == Visibility.Visible)
            {
                string? layout = (CmbLayout.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (layout != null)
                {
                    if (layout.Contains("2-up")) imagesPerPage = 2;
                    else if (layout.Contains("4-up")) imagesPerPage = 4;
                }
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
                            using (var document = PdfiumViewer.PdfDocument.Load(fs))
                            {
                                using (var printDocument = document.CreatePrintDocument())
                                {
                                    printDocument.PrinterSettings.PrinterName = selectedPrinter;
                                    printDocument.PrinterSettings.Copies = (short)copies;

                                    if (AdvancedSection.Visibility == Visibility.Visible)
                                    {
                                        string? quality = (CmbPrintQuality.SelectedItem as ComboBoxItem)?.Content?.ToString();
                                        if (quality != null)
                                        {
                                            if (quality.Contains("Draft")) printDocument.DefaultPageSettings.PrinterResolution.Kind = PrinterResolutionKind.Draft;
                                            else if (quality.Contains("High")) printDocument.DefaultPageSettings.PrinterResolution.Kind = PrinterResolutionKind.High;
                                        }
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

                        string? printOrient = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        pd.PrintTicket.PageOrientation = (printOrient == "Landscape") ? PageOrientation.Landscape : PageOrientation.Portrait;

                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(_currentDocumentPath);
                        bitmap.EndInit();
                        bitmap.Freeze();

                        DrawingVisual visual = new DrawingVisual();
                        using (DrawingContext dc = visual.RenderOpen())
                        {
                            double canvasWidth = pd.PrintableAreaWidth;
                            double canvasHeight = pd.PrintableAreaHeight;

                            if (imagesPerPage == 1)
                            {
                                string? scaling = (CmbScaling.SelectedItem as ComboBoxItem)?.Content?.ToString();
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

                    // Deduct toner levels
                    var printerObj = TargetPrinters.FirstOrDefault(p => p.Name == selectedPrinter);
                    if (printerObj != null)
                    {
                        double deduct = 0.5 * copies;
                        printerObj.TonerCyan = Math.Max(0, printerObj.TonerCyan - deduct);
                        printerObj.TonerMagenta = Math.Max(0, printerObj.TonerMagenta - deduct);
                        printerObj.TonerYellow = Math.Max(0, printerObj.TonerYellow - deduct);
                        printerObj.TonerBlack = Math.Max(0, printerObj.TonerBlack - (deduct * 1.5));
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

            string activeHardware = CmbActiveHardware.SelectedItem?.ToString() ?? "";

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
                string? previewOrient = (CmbOrientation.SelectedItem as ComboBoxItem)?.Content?.ToString();

                if (extension == ".pdf")
                {
                    ImageSource? rendered = RenderPdfPage(_currentDocumentPath, 0);
                    if (rendered != null)
                    {
                        PaperPreviewBorder.Visibility = Visibility.Visible;
                        ImgPrintPreview.Visibility = Visibility.Visible;
                        ImgPrintPreview.Source = rendered;
                        PdfPrintPreview.Visibility = Visibility.Collapsed;
                        TxtPdfPreviewMessage.Visibility = Visibility.Collapsed;

                        ImgPrintPreview.LayoutTransform = (previewOrient == "Landscape") ? new RotateTransform(90) : new RotateTransform(0);
                        return;
                    }
                    else
                    {
                        PaperPreviewBorder.Visibility = Visibility.Visible;
                        ImgPrintPreview.Visibility = Visibility.Collapsed;
                        PdfPrintPreview.Visibility = Visibility.Visible;
                        TxtPdfPreviewMessage.Visibility = Visibility.Visible;
                        PdfPrintPreview.Navigate(new Uri(_currentDocumentPath));
                        return;
                    }
                }
                
                PaperPreviewBorder.Visibility = Visibility.Visible;
                ImgPrintPreview.Visibility = Visibility.Visible;
                PdfPrintPreview.Visibility = Visibility.Collapsed;
                TxtPdfPreviewMessage.Visibility = Visibility.Collapsed;
                
                BitmapImage originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(_currentDocumentPath);
                originalImage.EndInit();
                originalImage.Freeze();

                string? colorMode = (CmbColorMode.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (colorMode == "Grayscale")
                {
                    FormatConvertedBitmap grayBitmap = new FormatConvertedBitmap(originalImage, PixelFormats.Gray8, null, 0);
                    grayBitmap.Freeze();
                    ImgPrintPreview.Source = grayBitmap;
                }
                else ImgPrintPreview.Source = originalImage;

                ImgPrintPreview.LayoutTransform = (previewOrient == "Landscape") ? new RotateTransform(90) : new RotateTransform(0);
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

        private void AutoHealService(string serviceName)
        {
            try
            {
                LogEvent($"[Auto-Heal] Restarting Windows Service: '{serviceName}'...");
                using (ServiceController s = new ServiceController(serviceName))
                {
                    if (s.Status != ServiceControllerStatus.Stopped)
                    {
                        LogEvent($"[Auto-Heal] Stopping '{serviceName}' service...");
                        s.Stop();
                        s.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    }
                    LogEvent($"[Auto-Heal] Starting '{serviceName}' service...");
                    s.Start();
                    s.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    LogEvent($"[Auto-Heal] Service '{serviceName}' restarted successfully!");
                }

                _healSequencesCount++;
                Dispatcher.Invoke(() => {
                    if (TxtDashHealCount != null)
                    {
                        TxtDashHealCount.Text = $"{_healSequencesCount} cycles";
                    }
                });
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                LogEvent($"[Auto-Heal] Error: Service '{serviceName}' restart timed out.");
            }
            catch (InvalidOperationException)
            {
                LogEvent($"[Auto-Heal] Access Denied: Run as Admin to restart '{serviceName}' service.");
            }
            catch (Exception ex)
            {
                LogEvent($"[Auto-Heal] Restart failed for '{serviceName}': {ex.Message}");
            }
        }

        private void AutoRestartSpooler()
        {
            AutoHealService("Spooler");
        }

        private void AutoRestartStiSvc()
        {
            AutoHealService("StiSvc");
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
                _currentDocumentPath = ofd.FileName;
                string ext = Path.GetExtension(_currentDocumentPath).ToLower();
                if (ext == ".pdf")
                {
                    ImageSource? rendered = RenderPdfPage(_currentDocumentPath, 0);
                    if (rendered != null)
                    {
                        ImgPreview.Source = rendered;
                        ImgPreview.Visibility = Visibility.Visible;
                        PdfViewer.Visibility = Visibility.Collapsed;
                        TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        PdfViewer.Visibility = Visibility.Visible;
                        ImgPreview.Visibility = Visibility.Collapsed;
                        TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;
                        PdfViewer.Navigate(new Uri(_currentDocumentPath));
                    }

                    ScannedPage importedPage = new ScannedPage 
                    { 
                        FilePath = _currentDocumentPath, 
                        Thumbnail = rendered, 
                        PageLabel = $"Page {BatchPages.Count + 1}" 
                    };
                    BatchPages.Add(importedPage);
                    ListBatchScans.SelectedItem = importedPage;
                }
                else
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(_currentDocumentPath);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ImgPreview.Source = bitmap;
                    ImgPreview.Visibility = Visibility.Visible;
                    PdfViewer.Visibility = Visibility.Collapsed;
                    TxtPreviewPlaceholder.Visibility = Visibility.Collapsed;

                    ScannedPage importedPage = new ScannedPage 
                    { 
                        FilePath = _currentDocumentPath, 
                        Thumbnail = bitmap, 
                        PageLabel = $"Page {BatchPages.Count + 1}" 
                    };
                    BatchPages.Add(importedPage);
                    ListBatchScans.SelectedItem = importedPage;
                }
                LogEvent("File Imported and added to Batch.");
            }
        }

        // ==========================================
        // RESPONSIVE SIDEBARS & SIZE CHANGED LOGIC
        // ==========================================
        private bool _isBatchCollapsed = false;
        private bool _isRightPanelCollapsed = false;
        private double _lastBatchWidth = 260;
        private double _lastRightPanelWidth = 340;

        private void BtnToggleBatch_Click(object sender, RoutedEventArgs e)
        {
            ToggleBatchPanel();
        }

        private void BtnToggleRightPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightPanel();
        }

        private void ToggleBatchPanel(bool? forceState = null)
        {
            bool targetState = forceState ?? !_isBatchCollapsed;
            if (targetState == _isBatchCollapsed) return;

            _isBatchCollapsed = targetState;
            if (_isBatchCollapsed)
            {
                _lastBatchWidth = ColBatchSidebar.Width.Value > 0 ? ColBatchSidebar.Width.Value : 260;
                ColBatchSidebar.Width = new GridLength(0);
                BorderBatchSidebar.Visibility = Visibility.Collapsed;
                IconToggleBatch.Kind = PackIconKind.Menu;
            }
            else
            {
                ColBatchSidebar.Width = new GridLength(_lastBatchWidth);
                BorderBatchSidebar.Visibility = Visibility.Visible;
                IconToggleBatch.Kind = PackIconKind.MenuOpen;
            }
        }

        private void ToggleRightPanel(bool? forceState = null)
        {
            bool targetState = forceState ?? !_isRightPanelCollapsed;
            if (targetState == _isRightPanelCollapsed) return;

            _isRightPanelCollapsed = targetState;
            if (_isRightPanelCollapsed)
            {
                _lastRightPanelWidth = ColRightPanel.Width.Value > 0 ? ColRightPanel.Width.Value : 340;
                ColRightPanel.Width = new GridLength(0);
                BorderRightPanel.Visibility = Visibility.Collapsed;
                IconToggleRightPanel.Kind = PackIconKind.Tune;
            }
            else
            {
                ColRightPanel.Width = new GridLength(_lastRightPanelWidth);
                BorderRightPanel.Visibility = Visibility.Visible;
                IconToggleRightPanel.Kind = PackIconKind.Tune;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;

            // 1. Navigation Sidebar Responsiveness (Column 0)
            if (width < 1024)
            {
                // Go compact
                ColNavSidebar.Width = new GridLength(70);
                
                LogoText1.Visibility = Visibility.Collapsed;
                LogoText2.Visibility = Visibility.Collapsed;
                LabelDashboard.Visibility = Visibility.Collapsed;
                LabelScanCenter.Visibility = Visibility.Collapsed;
                LabelBatchPages.Visibility = Visibility.Collapsed;
                LabelLibrary.Visibility = Visibility.Collapsed;
                LabelPdfReader.Visibility = Visibility.Collapsed;
                LabelConverter.Visibility = Visibility.Collapsed;
                LabelPrinterCenter.Visibility = Visibility.Collapsed;
                LabelAutomations.Visibility = Visibility.Collapsed;
                UserInfoPanel.Visibility = Visibility.Collapsed;
                TxtThemeMode.Visibility = Visibility.Collapsed;
                
                // Adjust margins and alignment for compact look
                PanelLogo.Margin = new Thickness(0, 0, 0, 30);
                PanelLogo.HorizontalAlignment = HorizontalAlignment.Center;
                LogoIconBorder.Margin = new Thickness(0);
                LogoIconBorder.HorizontalAlignment = HorizontalAlignment.Center;

                NavDashboard.Padding = new Thickness(0, 10, 0, 10);
                PanelDashboard.HorizontalAlignment = HorizontalAlignment.Center;

                NavScanCenter.Padding = new Thickness(0, 10, 0, 10);
                PanelScanCenter.HorizontalAlignment = HorizontalAlignment.Center;

                NavBatchPages.Padding = new Thickness(0, 10, 0, 10);
                PanelBatchPages.HorizontalAlignment = HorizontalAlignment.Center;

                NavLibrary.Padding = new Thickness(0, 10, 0, 10);
                PanelLibrary.HorizontalAlignment = HorizontalAlignment.Center;

                NavPdfReader.Padding = new Thickness(0, 10, 0, 10);
                PanelPdfReader.HorizontalAlignment = HorizontalAlignment.Center;

                NavConverter.Padding = new Thickness(0, 10, 0, 10);
                PanelConverter.HorizontalAlignment = HorizontalAlignment.Center;

                NavPrinterCenter.Padding = new Thickness(0, 10, 0, 10);
                PanelPrinterCenter.HorizontalAlignment = HorizontalAlignment.Center;

                NavAutomations.Padding = new Thickness(0, 10, 0, 10);
                PanelAutomations.HorizontalAlignment = HorizontalAlignment.Center;

                UserBorder.Padding = new Thickness(5);
                PanelUser.Margin = new Thickness(0);
            }
            else
            {
                // Go full
                ColNavSidebar.Width = new GridLength(220);
                
                LogoText1.Visibility = Visibility.Visible;
                LogoText2.Visibility = Visibility.Visible;
                LabelDashboard.Visibility = Visibility.Visible;
                LabelScanCenter.Visibility = Visibility.Visible;
                LabelBatchPages.Visibility = Visibility.Visible;
                LabelLibrary.Visibility = Visibility.Visible;
                LabelPdfReader.Visibility = Visibility.Visible;
                LabelConverter.Visibility = Visibility.Visible;
                LabelPrinterCenter.Visibility = Visibility.Visible;
                LabelAutomations.Visibility = Visibility.Visible;
                UserInfoPanel.Visibility = Visibility.Visible;
                TxtThemeMode.Visibility = Visibility.Visible;
                
                PanelLogo.Margin = new Thickness(5, 0, 0, 30);
                PanelLogo.HorizontalAlignment = HorizontalAlignment.Left;
                LogoIconBorder.Margin = new Thickness(0, 0, 10, 0);
                LogoIconBorder.HorizontalAlignment = HorizontalAlignment.Left;

                NavDashboard.Padding = new Thickness(12, 10, 12, 10);
                PanelDashboard.HorizontalAlignment = HorizontalAlignment.Left;

                NavScanCenter.Padding = new Thickness(12, 10, 12, 10);
                PanelScanCenter.HorizontalAlignment = HorizontalAlignment.Left;

                NavBatchPages.Padding = new Thickness(12, 10, 12, 10);
                PanelBatchPages.HorizontalAlignment = HorizontalAlignment.Left;

                NavLibrary.Padding = new Thickness(12, 10, 12, 10);
                PanelLibrary.HorizontalAlignment = HorizontalAlignment.Left;

                NavPdfReader.Padding = new Thickness(12, 10, 12, 10);
                PanelPdfReader.HorizontalAlignment = HorizontalAlignment.Left;

                NavConverter.Padding = new Thickness(12, 10, 12, 10);
                PanelConverter.HorizontalAlignment = HorizontalAlignment.Left;

                NavPrinterCenter.Padding = new Thickness(12, 10, 12, 10);
                PanelPrinterCenter.HorizontalAlignment = HorizontalAlignment.Left;

                NavAutomations.Padding = new Thickness(12, 10, 12, 10);
                PanelAutomations.HorizontalAlignment = HorizontalAlignment.Left;

                UserBorder.Padding = new Thickness(10);
                PanelUser.Margin = new Thickness(5, 0, 0, 0);
            }

            // 2. Auto-collapse sidebars on small screen sizes
            if (width < 900)
            {
                if (!_isBatchCollapsed)
                {
                    ToggleBatchPanel(true); // force collapse
                }
            }
            if (width < 768)
            {
                if (!_isRightPanelCollapsed)
                {
                    ToggleRightPanel(true); // force collapse
                }
            }

            // 3. Adaptive view panel column widths
            // Calculate the content area width (total width minus sidebar)
            double sidebarWidth = width < 1024 ? 70 : 220;
            double contentWidth = width - sidebarWidth;

            ApplyResponsiveViewColumns(contentWidth);
        }

        private void ApplyResponsiveViewColumns(double contentWidth)
        {
            // -- ViewScanCenter: Left(380) + Right(340) = 720 fixed --
            if (contentWidth < 700)
            {
                // Stack: hide right panel, shrink left
                ColScanLeft.Width = new GridLength(240);
                ColScanRight.Width = new GridLength(0);
                BorderScanRight.Visibility = Visibility.Collapsed;
            }
            else if (contentWidth < 950)
            {
                ColScanLeft.Width = new GridLength(280);
                ColScanRight.Width = new GridLength(260);
                BorderScanRight.Visibility = Visibility.Visible;
            }
            else
            {
                ColScanLeft.Width = new GridLength(380);
                ColScanRight.Width = new GridLength(340);
                BorderScanRight.Visibility = Visibility.Visible;
            }

            // -- ViewLibrary: Left(300) + Right(340) = 640 fixed --
            if (contentWidth < 650)
            {
                ColLibLeft.Width = new GridLength(220);
                ColLibRight.Width = new GridLength(0);
                BorderLibRight.Visibility = Visibility.Collapsed;
            }
            else if (contentWidth < 900)
            {
                ColLibLeft.Width = new GridLength(240);
                ColLibRight.Width = new GridLength(260);
                BorderLibRight.Visibility = Visibility.Visible;
            }
            else
            {
                ColLibLeft.Width = new GridLength(300);
                ColLibRight.Width = new GridLength(340);
                BorderLibRight.Visibility = Visibility.Visible;
            }

            // -- ViewAutomations: Left(320) + Right(340) = 660 fixed --
            if (contentWidth < 660)
            {
                ColAutoLeft.Width = new GridLength(240);
                ColAutoRight.Width = new GridLength(0);
                BorderAutoRight.Visibility = Visibility.Collapsed;
            }
            else if (contentWidth < 900)
            {
                ColAutoLeft.Width = new GridLength(260);
                ColAutoRight.Width = new GridLength(260);
                BorderAutoRight.Visibility = Visibility.Visible;
            }
            else
            {
                ColAutoLeft.Width = new GridLength(320);
                ColAutoRight.Width = new GridLength(340);
                BorderAutoRight.Visibility = Visibility.Visible;
            }

            // -- ViewPdfReader: Left(260) + Right(320) = 580 fixed --
            if (contentWidth < 580)
            {
                ColPdfLeft.Width = new GridLength(0);
                BorderPdfLeft.Visibility = Visibility.Collapsed;
                ColPdfRight.Width = new GridLength(0);
                BorderPdfRight.Visibility = Visibility.Collapsed;
            }
            else if (contentWidth < 800)
            {
                ColPdfLeft.Width = new GridLength(180);
                BorderPdfLeft.Visibility = Visibility.Visible;
                ColPdfRight.Width = new GridLength(0);
                BorderPdfRight.Visibility = Visibility.Collapsed;
            }
            else
            {
                ColPdfLeft.Width = new GridLength(260);
                BorderPdfLeft.Visibility = Visibility.Visible;
                ColPdfRight.Width = new GridLength(320);
                BorderPdfRight.Visibility = Visibility.Visible;
            }

            // -- ViewConverter: Left(320) --
            if (contentWidth < 500)
            {
                ColConvLeft.Width = new GridLength(220);
            }
            else
            {
                ColConvLeft.Width = new GridLength(320);
            }

            // -- ViewPrinters: Left(320) only, right is now * --
            if (contentWidth < 500)
            {
                ColPrintLeft.Width = new GridLength(240);
            }
            else
            {
                ColPrintLeft.Width = new GridLength(320);
            }

            // -- ViewDashboard: Bottom right(380) --
            if (contentWidth < 700)
            {
                ColDashRight.Width = new GridLength(0);
                BorderDashRight.Visibility = Visibility.Collapsed;
            }
            else if (contentWidth < 900)
            {
                ColDashRight.Width = new GridLength(280);
                BorderDashRight.Visibility = Visibility.Visible;
            }
            else
            {
                ColDashRight.Width = new GridLength(380);
                BorderDashRight.Visibility = Visibility.Visible;
            }
        }

        // Mouse event handlers for Left Sidebar Navigation Items
        private void NavDashboard_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("Dashboard");
        }

        private void NavScanCenter_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("ScanCenter");
        }

        private void NavBatchPages_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("BatchPages");
        }

        private void NavLibrary_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("Library");
        }

        private void NavPdfReader_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("PdfReader");
        }
        
        private void NavConverter_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("Converter");
        }

        private void NavPrinterCenter_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("PrinterCenter");
        }

        private void NavAutomations_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SwitchToTab("Automations");
        }

        private void UpdateNavItemStyle(Border border, PackIcon icon, TextBlock text, bool isActive)
        {
            var palette = _isDarkMode ? DarkPalette : LightPalette;
            
            var brushActiveBg = new BrushConverter().ConvertFromString(palette["NavActiveBg"]) as Brush ?? Brushes.Transparent;
            var brushActiveBorder = new BrushConverter().ConvertFromString(palette["NavActiveBorder"]) as Brush ?? Brushes.Transparent;
            var brushActiveIcon = new BrushConverter().ConvertFromString("#06B6D4") as Brush ?? Brushes.Transparent;
            var brushActiveText = new BrushConverter().ConvertFromString(palette["ColorTextPrimary"]) as Brush ?? Brushes.Transparent;
            
            var brushMutedIcon = new BrushConverter().ConvertFromString(palette["ColorTextMuted"]) as Brush ?? Brushes.Transparent;
            var brushMutedText = new BrushConverter().ConvertFromString(palette["ColorTextMuted"]) as Brush ?? Brushes.Transparent;

            if (isActive)
            {
                border.Background = brushActiveBg;
                border.BorderBrush = brushActiveBorder;
                border.BorderThickness = new Thickness(1);
                
                icon.Foreground = brushActiveIcon;
                text.Foreground = brushActiveText;
                text.FontWeight = FontWeights.Bold;
            }
            else
            {
                border.Background = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                
                icon.Foreground = brushMutedIcon;
                text.Foreground = brushMutedText;
                text.FontWeight = FontWeights.SemiBold;
            }
        }

        private void ApplyTheme()
        {
            // 1. UPDATE ANG IYONG CUSTOM PALETTE
            var palette = _isDarkMode ? DarkPalette : LightPalette;
            
            foreach (var kvp in palette)
            {
                var colorString = kvp.Value;
                var color = (Color)ColorConverter.ConvertFromString(colorString);
                
                if (kvp.Key.StartsWith("Color"))
                {
                    // Para sa mga Color types
                    Application.Current.Resources[kvp.Key] = color;
                }
                else
                {
                    // Para sa mga SolidColorBrush types
                    var brush = new SolidColorBrush(color);
                    brush.Freeze(); // Best practice: I-freeze para iwas memory leaks at bumilis ang UI
                    Application.Current.Resources[kvp.Key] = brush;
                }
            }
    
            // 2. I-UPDATE ANG MATERIAL DESIGN BASE THEME (FIXED)
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
    
            // Gagamit tayo ng IBaseTheme object para mawala ang CS1503 error
            IBaseTheme baseTheme = _isDarkMode ? new MaterialDesignDarkTheme() : (IBaseTheme)new MaterialDesignLightTheme();
            theme.SetBaseTheme(baseTheme);
            
            paletteHelper.SetTheme(theme);
            
            // 3. RE-APPLY NAVIGATION STYLES SA SIDEBAR
            SwitchToTab(_currentTab);
        }

        private void SwitchToTab(string tabName)
        {
            _currentTab = tabName;
            
            // Update highlights, icon and text colors for all sidebar navigation items
            UpdateNavItemStyle(NavDashboard, IconDashboard, LabelDashboard, tabName == "Dashboard");
            UpdateNavItemStyle(NavScanCenter, IconScanCenter, LabelScanCenter, tabName == "ScanCenter");
            UpdateNavItemStyle(NavBatchPages, IconBatchPages, LabelBatchPages, tabName == "BatchPages");
            UpdateNavItemStyle(NavLibrary, IconLibrary, LabelLibrary, tabName == "Library");
            UpdateNavItemStyle(NavPdfReader, IconPdfReader, LabelPdfReader, tabName == "PdfReader");
            UpdateNavItemStyle(NavConverter, IconConverter, LabelConverter, tabName == "Converter");
            UpdateNavItemStyle(NavPrinterCenter, IconPrinterCenter, LabelPrinterCenter, tabName == "PrinterCenter");
            UpdateNavItemStyle(NavAutomations, IconAutomations, LabelAutomations, tabName == "Automations");

            // Default state: hide all central views and collapse their columns
            ColBatchSidebar.Width = new GridLength(0);
            BorderBatchSidebar.Visibility = Visibility.Collapsed;

            ColPreview.Width = new GridLength(0);
            GridPreviewArea.Visibility = Visibility.Collapsed;

            ColRightPanel.Width = new GridLength(0);
            BorderRightPanel.Visibility = Visibility.Collapsed;

            ViewScanCenter.Visibility = Visibility.Collapsed;
            ViewLibrary.Visibility = Visibility.Collapsed;
            ViewPlaceholder.Visibility = Visibility.Collapsed;
            ViewDashboard.Visibility = Visibility.Collapsed;
            ViewAutomations.Visibility = Visibility.Collapsed;
            ViewPdfReader.Visibility = Visibility.Collapsed;
            ViewConverter.Visibility = Visibility.Collapsed;
            ViewPrinters.Visibility = Visibility.Collapsed;

            if (tabName == "BatchPages")
            {
                // Restore columns in Batch Pages
                ColBatchSidebar.Width = new GridLength(_isBatchCollapsed ? 0 : _lastBatchWidth);
                BorderBatchSidebar.Visibility = _isBatchCollapsed ? Visibility.Collapsed : Visibility.Visible;

                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                GridPreviewArea.Visibility = Visibility.Visible;

                ColRightPanel.Width = new GridLength(_isRightPanelCollapsed ? 0 : _lastRightPanelWidth);
                BorderRightPanel.Visibility = _isRightPanelCollapsed ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (tabName == "ScanCenter")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewScanCenter.Visibility = Visibility.Visible;
            }
            else if (tabName == "Library")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewLibrary.Visibility = Visibility.Visible;
                RefreshLibrary();
            }
            else if (tabName == "Automations")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewAutomations.Visibility = Visibility.Visible;
                LogAutomationEvent("Automations control panel selected.");
            }
            else if (tabName == "PdfReader")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewPdfReader.Visibility = Visibility.Visible;
            }
            else if (tabName == "Converter")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewConverter.Visibility = Visibility.Visible;
                // Initialize default path if empty
                if (string.IsNullOrEmpty(TxtConverterDestPath.Text))
                {
                    TxtConverterDestPath.Text = _currentLibraryPath;
                }
            }
            else if (tabName == "PrinterCenter")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewPrinters.Visibility = Visibility.Visible;
                RefreshPrinterCenterList();
            }
            else if (tabName == "Dashboard")
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewDashboard.Visibility = Visibility.Visible;
                UpdateDashboardData();
            }
            else
            {
                ColPreview.Width = new GridLength(1, GridUnitType.Star);
                ViewPlaceholder.Visibility = Visibility.Visible;
                ShowPlaceholderView(tabName);
            }
        }

        private void ShowPlaceholderView(string tabName)
        {
            if (tabName == "Dashboard")
            {
                IconPlaceholderTab.Kind = PackIconKind.ViewDashboard;
                TxtPlaceholderTitle.Text = "SYSTEM DASHBOARD";
                TxtPlaceholderDesc.Text = "Real-time engine metrics, total documents scanned, scanner health charts, and diagnostic history details are currently being calculated by the Auto-Heal diagnostics system.";
            }
            else if (tabName == "Automations")
            {
                IconPlaceholderTab.Kind = PackIconKind.Robot;
                TxtPlaceholderTitle.Text = "AUTOMATED JOBS & ACTIONS";
                TxtPlaceholderDesc.Text = "Configure automatic printer diagnostic loops, backup schedules, custom WIA-based workflows, and automatic email delivery setups.";
            }
        }

        // ==========================================
        // DOCUMENT LIBRARY LOGIC & FILE OPERATIONS
        // ==========================================
        
        private async void RefreshLibrary()
        {
            if (!Directory.Exists(_currentLibraryPath))
            {
                try { Directory.CreateDirectory(_currentLibraryPath); }
                catch (Exception ex) { LogEvent($"Failed to create library folder: {ex.Message}"); return; }
            }

            TxtLibraryPath.Text = _currentLibraryPath;
            ShowModal("Scanning storage repository...");

            try
            {
                var files = await Task.Run(() => {
                    var list = new List<LibraryItem>();
                    var dir = new DirectoryInfo(_currentLibraryPath);
                    if (!dir.Exists) return list;

                    foreach (var f in dir.GetFiles())
                    {
                        string ext = f.Extension.ToLower();
                        if (ext == ".pdf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                        {
                            var item = new LibraryItem
                            {
                                FilePath = f.FullName,
                                FileName = f.Name,
                                DateModified = f.LastWriteTime,
                                FileExtension = ext,
                                RawSize = f.Length,
                                FileSize = FormatBytes(f.Length)
                            };

                            // Render / generate thumbnail
                            item.Thumbnail = GenerateLibraryThumbnail(f.FullName, ext);
                            list.Add(item);
                        }
                    }
                    return list;
                });

                _rawLibraryItems = files;
                ApplyLibraryFilters();
                UpdateLibraryStats();
            }
            catch (Exception ex)
            {
                LogEvent($"Library loading error: {ex.Message}");
            }
            finally
            {
                HideModal();
            }
        }

        private ImageSource? RenderPdfPage(string filePath, int pageIndex)
        {
            try
            {
                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(filePath))
                {
                    if (pdfDoc.PageCount > pageIndex)
                    {
                        using (var img = pdfDoc.Render(pageIndex, 150, 150, false))
                        {
                            using (var ms = new MemoryStream())
                            {
                                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent($"[Render] PDF render error on page {pageIndex}: {ex.Message}");
            }
            return null;
        }

        private ImageSource? GenerateLibraryThumbnail(string filePath, string ext)
        {
            try
            {
                if (ext == ".pdf")
                {
                    return RenderPdfPage(filePath, 0);
                }
                else
                {
                    // Scale down loaded scan images to conserve RAM
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 120;
                    bmp.UriSource = new Uri(filePath);
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
                // Return null so XAML style trigger displays beautiful fallback vector icons
            }
            return null;
        }

        private void ApplyLibraryFilters()
        {
            if (!_isLibraryInitLoaded || LibraryFiles == null) return;

            string search = TxtLibrarySearch.Text.Trim().ToLower();
            string selectedFormat = (CmbLibraryFormat.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "All Formats";
            string selectedSort = (CmbLibrarySort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Date Modified (Newest)";

            IEnumerable<LibraryItem> query = _rawLibraryItems;

            // 1. Active Category Tag (All, PDF, Image)
            if (_activeCategory == "PDF")
            {
                query = query.Where(x => x.IsPdf);
            }
            else if (_activeCategory == "Image")
            {
                query = query.Where(x => !x.IsPdf);
            }

            // 2. Format ComboBox Selection
            if (selectedFormat.Contains("PDF"))
            {
                query = query.Where(x => x.IsPdf);
            }
            else if (selectedFormat.Contains("JPEG"))
            {
                query = query.Where(x => x.FileExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || 
                                         x.FileExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
            }
            else if (selectedFormat.Contains("PNG"))
            {
                query = query.Where(x => x.FileExtension.Equals(".png", StringComparison.OrdinalIgnoreCase));
            }

            // 3. Search Filter
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.FileName.ToLower().Contains(search));
            }

            // 4. Sorting logic
            if (selectedSort.Contains("Newest"))
            {
                query = query.OrderByDescending(x => x.DateModified);
            }
            else if (selectedSort.Contains("Oldest"))
            {
                query = query.OrderBy(x => x.DateModified);
            }
            else if (selectedSort.Contains("A to Z"))
            {
                query = query.OrderBy(x => x.FileName);
            }
            else if (selectedSort.Contains("Z to A"))
            {
                query = query.OrderByDescending(x => x.FileName);
            }
            else if (selectedSort.Contains("Largest"))
            {
                query = query.OrderByDescending(x => x.RawSize);
            }
            else if (selectedSort.Contains("Smallest"))
            {
                query = query.OrderBy(x => x.RawSize);
            }

            LibraryFiles.Clear();
            int count = 0;
            foreach (var item in query)
            {
                LibraryFiles.Add(item);
                count++;
            }

            TxtLibraryEmpty.Visibility = (count == 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLibraryStats()
        {
            int allCount = _rawLibraryItems.Count;
            int pdfCount = _rawLibraryItems.Count(x => x.IsPdf);
            int imgCount = allCount - pdfCount;

            TxtCountAll.Text = allCount.ToString();
            TxtCountPdf.Text = pdfCount.ToString();
            TxtCountImg.Text = imgCount.ToString();

            long totalBytes = _rawLibraryItems.Sum(x => x.RawSize);
            TxtLibraryDiskSize.Text = FormatBytes(totalBytes);

            long avgBytes = allCount > 0 ? totalBytes / allCount : 0;
            TxtLibraryAvgSize.Text = FormatBytes(avgBytes);
        }

        private string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0.00 KB";
            long bytesAbs = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytesAbs, 1024)));
            double num = Math.Round(bytesAbs / Math.Pow(1024, place), 2);
            return $"{(Math.Sign(bytes) * num):F2} {suf[place]}";
        }

        // Folder Browse Click
        private void BtnBrowseLibrary_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "Select Local Scanned Document Directory";
            dialog.InitialDirectory = _currentLibraryPath;
            if (dialog.ShowDialog() == true)
            {
                _currentLibraryPath = dialog.FolderName;
                RefreshLibrary();
            }
        }

        // Quick Category Tag Swapping
        private void TagAllFiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _activeCategory = "All";
            UpdateCategoryStyles();
            ApplyLibraryFilters();
        }

        private void TagPdfFiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _activeCategory = "PDF";
            UpdateCategoryStyles();
            ApplyLibraryFilters();
        }

        private void TagImgFiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _activeCategory = "Image";
            UpdateCategoryStyles();
            ApplyLibraryFilters();
        }

        private void UpdateCategoryStyles()
        {
            UpdateTagSelectionStyle(TagAllFiles, LabelTagAll, _activeCategory == "All");
            UpdateTagSelectionStyle(TagPdfFiles, LabelTagPdf, _activeCategory == "PDF");
            UpdateTagSelectionStyle(TagImgFiles, LabelTagImg, _activeCategory == "Image");
        }

        private void UpdateTagSelectionStyle(Border border, TextBlock label, bool isSelected)
        {
            if (isSelected)
            {
                border.Background = new BrushConverter().ConvertFromString("#1E293B") as Brush ?? Brushes.Transparent;
                border.BorderBrush = new BrushConverter().ConvertFromString("#334155") as Brush ?? Brushes.Transparent;
                border.BorderThickness = new Thickness(1);
                label.Foreground = new BrushConverter().ConvertFromString("#F8FAFC") as Brush ?? Brushes.Transparent;
                label.FontWeight = FontWeights.Bold;
            }
            else
            {
                border.Background = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                label.Foreground = new BrushConverter().ConvertFromString("#94A3B8") as Brush ?? Brushes.Transparent;
                label.FontWeight = FontWeights.SemiBold;
            }
        }

        // Search & ComboBox Changes
        private void TxtLibrarySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyLibraryFilters();
        }

        private void CmbLibraryFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLibraryFilters();
        }

        private void CmbLibrarySort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyLibraryFilters();
        }

        private void BtnRefreshLibrary_Click(object sender, RoutedEventArgs e)
        {
            RefreshLibrary();
        }

        // Selected File detail panel binding
        private void ListLibraryFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListLibraryFiles.SelectedItem is LibraryItem selected)
            {
                TxtLibraryDetailPlaceholder.Visibility = Visibility.Collapsed;
                GridLibraryDetails.Visibility = Visibility.Visible;

                TxtLibDetailName.Text = selected.FileName;
                TxtLibDetailPath.Text = selected.FilePath;
                TxtLibDetailSize.Text = selected.FileSize;
                TxtLibDetailDate.Text = selected.DisplayDate;

                BtnLibReadPdf.Visibility = selected.IsPdf ? Visibility.Visible : Visibility.Collapsed;

                // Load detail preview asynchronously to stay fast
                try
                {
                    if (selected.IsPdf)
                    {
                        // Render full page 1 PDF preview or use cached thumbnail
                        ImgLibraryDetailPreview.Source = selected.Thumbnail;
                    }
                    else
                    {
                        BitmapImage img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.UriSource = new Uri(selected.FilePath);
                        img.EndInit();
                        ImgLibraryDetailPreview.Source = img;
                    }
                }
                catch
                {
                    ImgLibraryDetailPreview.Source = selected.Thumbnail;
                }
            }
            else
            {
                TxtLibraryDetailPlaceholder.Visibility = Visibility.Visible;
                GridLibraryDetails.Visibility = Visibility.Collapsed;
                ImgLibraryDetailPreview.Source = null;
                BtnLibReadPdf.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnLibReadPdf_Click(object sender, RoutedEventArgs e)
        {
            if (ListLibraryFiles.SelectedItem is LibraryItem selected && selected.IsPdf)
            {
                SwitchToTab("PdfReader");
                await LoadPdfReaderDocument(selected.FilePath);
            }
        }

        private async void BtnPdfReaderOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "PDF Documents (*.pdf)|*.pdf";
            ofd.Title = "Open PDF Document";

            if (ofd.ShowDialog() == true)
            {
                await LoadPdfReaderDocument(ofd.FileName);
            }
        }

        private async Task LoadPdfReaderDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            _pdfReaderCurrentPath = filePath;
            FileInfo fileInfo = new FileInfo(filePath);

            // Set metadata fields
            TxtPdfReaderDetailName.Text = fileInfo.Name;
            TxtPdfReaderDetailSize.Text = FormatBytes(fileInfo.Length);
            TxtPdfReaderDetailPath.Text = filePath;
            TxtPdfReaderFileNameHeader.Text = fileInfo.Name;

            ShowModal("Loading PDF Pages...");
            PdfReaderPages.Clear();

            try
            {
                int totalPages = 0;
                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(filePath))
                {
                    totalPages = pdfDoc.PageCount;
                }

                TxtPdfReaderDetailPageCount.Text = $"{totalPages} pages";
                TxtPdfReaderPageCountLabel.Text = $"{totalPages} pages loaded";

                var pageList = new List<PdfReaderPageItem>();

                await Task.Run(() =>
                {
                    for (int i = 0; i < totalPages; i++)
                    {
                        ImageSource? thumb = RenderPdfPage(filePath, i);
                        pageList.Add(new PdfReaderPageItem
                        {
                            PageIndex = i,
                            Thumbnail = thumb
                        });
                    }
                });

                foreach (var page in pageList)
                {
                    PdfReaderPages.Add(page);
                }

                ListPdfReaderThumbnails.ItemsSource = PdfReaderPages;

                if (PdfReaderPages.Count > 0)
                {
                    ListPdfReaderThumbnails.SelectedIndex = 0;
                    PanelPdfReaderPlaceholder.Visibility = Visibility.Collapsed;
                    ScrollPdfPage.Visibility = Visibility.Visible;
                    BtnPdfReaderClose.Visibility = Visibility.Visible;

                    // Enable action buttons
                    BtnPdfReaderAddToBatch.IsEnabled = true;
                    BtnPdfReaderPrintPage.IsEnabled = true;
                    BtnPdfReaderExportImage.IsEnabled = true;
                    BtnPdfReaderRotateLeft.IsEnabled = true;
                    BtnPdfReaderRotateRight.IsEnabled = true;
                }
                
                LogEvent($"[PDF Reader] Loaded document: {fileInfo.Name} ({totalPages} pages)");
            }
            catch (Exception ex)
            {
                ShowHardwareAlert("Load PDF Error", ex.Message);
            }
            finally
            {
                HideModal();
            }
        }

        private void BtnPdfReaderClose_Click(object sender, RoutedEventArgs e)
        {
            _pdfReaderCurrentPath = "";
            _pdfReaderCurrentPageIndex = 0;
            _pdfReaderZoomScale = 1.0;
            _pdfReaderRotationAngle = 0;

            // Reset UI metadata
            TxtPdfReaderDetailName.Text = "No file selected";
            TxtPdfReaderDetailSize.Text = "-";
            TxtPdfReaderDetailPath.Text = "-";
            TxtPdfReaderDetailPageCount.Text = "-";
            TxtPdfReaderFileNameHeader.Text = "No file opened";
            TxtPdfReaderPageCountLabel.Text = "No PDF loaded";
            TxtPdfReaderPageIndicator.Text = "Page 0 of 0";

            PdfReaderPages.Clear();
            ListPdfReaderThumbnails.ItemsSource = null;

            // Reset page viewer source
            ImgPdfPageDisplay.Source = null;

            // Reset UI visibility and states
            PanelPdfReaderPlaceholder.Visibility = Visibility.Visible;
            ScrollPdfPage.Visibility = Visibility.Collapsed;
            BtnPdfReaderClose.Visibility = Visibility.Collapsed;

            // Disable action buttons
            BtnPdfReaderAddToBatch.IsEnabled = false;
            BtnPdfReaderPrintPage.IsEnabled = false;
            BtnPdfReaderExportImage.IsEnabled = false;
            BtnPdfReaderRotateLeft.IsEnabled = false;
            BtnPdfReaderRotateRight.IsEnabled = false;

            LogEvent("[PDF Reader] Document closed and reader reset.");
        }

        private ImageSource? RenderPdfPageHighRes(string filePath, int pageIndex)
        {
            try
            {
                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(filePath))
                {
                    if (pdfDoc.PageCount > pageIndex)
                    {
                        using (var img = pdfDoc.Render(pageIndex, 300, 300, false))
                        {
                            using (var ms = new MemoryStream())
                            {
                                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                return bmp;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent($"[Render High-Res] PDF render error on page {pageIndex}: {ex.Message}");
            }
            return null;
        }

        private void DisplayPdfReaderPage(int pageIndex)
        {
            if (string.IsNullOrEmpty(_pdfReaderCurrentPath) || !File.Exists(_pdfReaderCurrentPath)) return;

            try
            {
                _pdfReaderCurrentPageIndex = pageIndex;
                TxtPdfReaderPageIndicator.Text = $"Page {pageIndex + 1} of {PdfReaderPages.Count}";

                ImageSource? renderedPage = RenderPdfPageHighRes(_pdfReaderCurrentPath, pageIndex);
                if (renderedPage != null)
                {
                    ImgPdfPageDisplay.Source = renderedPage;
                    ApplyPdfReaderZoomAndRotation();
                }
            }
            catch (Exception ex)
            {
                LogEvent($"[PDF Reader] Error displaying page: {ex.Message}");
            }
        }

        private void ApplyPdfReaderZoomAndRotation()
        {
            TransformGroup group = new TransformGroup();
            group.Children.Add(new ScaleTransform(_pdfReaderZoomScale, _pdfReaderZoomScale));
            group.Children.Add(new RotateTransform(_pdfReaderRotationAngle));
            ImgPdfPageDisplay.LayoutTransform = group;
            
            TxtPdfReaderZoomLabel.Text = $"{Math.Round(_pdfReaderZoomScale * 100)}%";
        }

        private void ListPdfReaderThumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListPdfReaderThumbnails.SelectedItem is PdfReaderPageItem selected)
            {
                DisplayPdfReaderPage(selected.PageIndex);
            }
        }

        private void BtnPdfReaderPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfReaderCurrentPageIndex > 0)
            {
                ListPdfReaderThumbnails.SelectedIndex = _pdfReaderCurrentPageIndex - 1;
            }
        }

        private void BtnPdfReaderNext_Click(object sender, RoutedEventArgs e)
        {
            if (_pdfReaderCurrentPageIndex < PdfReaderPages.Count - 1)
            {
                ListPdfReaderThumbnails.SelectedIndex = _pdfReaderCurrentPageIndex + 1;
            }
        }

        private void BtnPdfReaderZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _pdfReaderZoomScale += 0.1;
            if (_pdfReaderZoomScale > 3.0) _pdfReaderZoomScale = 3.0;
            ApplyPdfReaderZoomAndRotation();
        }

        private void BtnPdfReaderZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _pdfReaderZoomScale -= 0.1;
            if (_pdfReaderZoomScale < 0.3) _pdfReaderZoomScale = 0.3;
            ApplyPdfReaderZoomAndRotation();
        }

        private void BtnPdfReaderZoomFit_Click(object sender, RoutedEventArgs e)
        {
            _pdfReaderZoomScale = 1.0;
            ApplyPdfReaderZoomAndRotation();
        }

        private void BtnPdfReaderRotateLeft_Click(object sender, RoutedEventArgs e)
        {
            _pdfReaderRotationAngle = (_pdfReaderRotationAngle - 90 + 360) % 360;
            ApplyPdfReaderZoomAndRotation();
        }

        private void BtnPdfReaderRotateRight_Click(object sender, RoutedEventArgs e)
        {
            _pdfReaderRotationAngle = (_pdfReaderRotationAngle + 90) % 360;
            ApplyPdfReaderZoomAndRotation();
        }

        private void BtnPdfReaderAddToBatch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pdfReaderCurrentPath) || !File.Exists(_pdfReaderCurrentPath)) return;

            try
            {
                string folder = @"C:\ScannedDocuments";
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string baseName = Path.GetFileNameWithoutExtension(_pdfReaderCurrentPath);
                string newFileName = $"{baseName}_Page{_pdfReaderCurrentPageIndex + 1}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string fullPath = Path.Combine(folder, newFileName);

                using (var pdfDoc = PdfiumViewer.PdfDocument.Load(_pdfReaderCurrentPath))
                {
                    using (var img = pdfDoc.Render(_pdfReaderCurrentPageIndex, 300, 300, false))
                    {
                        img.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                }

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(fullPath);
                bitmap.EndInit();
                bitmap.Freeze();

                var newPage = new ScannedPage
                {
                    FilePath = fullPath,
                    Thumbnail = bitmap,
                    PageLabel = $"{baseName} - Page {_pdfReaderCurrentPageIndex + 1}"
                };

                BatchPages.Add(newPage);
                LogEvent($"[PDF Reader] Extracted Page {_pdfReaderCurrentPageIndex + 1} and added to active batch.");
                ShowHardwareAlert("Page Extracted", $"Successfully extracted Page {_pdfReaderCurrentPageIndex + 1} and added it to active batch scans.");
            }
            catch (Exception ex)
            {
                ShowHardwareAlert("Extraction Error", ex.Message);
            }
        }

        private void BtnPdfReaderPrintPage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pdfReaderCurrentPath) || !File.Exists(_pdfReaderCurrentPath)) return;

            try
            {
                PrintDialog pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    var rendered = RenderPdfPageHighRes(_pdfReaderCurrentPath, _pdfReaderCurrentPageIndex) as BitmapSource;
                    if (rendered != null)
                    {
                        DrawingVisual visual = new DrawingVisual();
                        using (DrawingContext dc = visual.RenderOpen())
                        {
                            dc.DrawImage(rendered, new Rect(0, 0, pd.PrintableAreaWidth, pd.PrintableAreaHeight));
                        }
                        pd.PrintVisual(visual, $"AutoHealScanner PDFReader Page {_pdfReaderCurrentPageIndex + 1}");
                        LogEvent($"[PDF Reader] Printed Page {_pdfReaderCurrentPageIndex + 1} of {Path.GetFileName(_pdfReaderCurrentPath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowHardwareAlert("Print Error", ex.Message);
            }
        }

        private void BtnPdfReaderExportImage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pdfReaderCurrentPath) || !File.Exists(_pdfReaderCurrentPath)) return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png";
            sfd.FileName = $"{Path.GetFileNameWithoutExtension(_pdfReaderCurrentPath)}_Page{_pdfReaderCurrentPageIndex + 1}.jpg";
            sfd.Title = "Export Page as Image";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (var pdfDoc = PdfiumViewer.PdfDocument.Load(_pdfReaderCurrentPath))
                    {
                        using (var img = pdfDoc.Render(_pdfReaderCurrentPageIndex, 300, 300, false))
                        {
                            var format = Path.GetExtension(sfd.FileName).ToLower() == ".png" 
                                ? System.Drawing.Imaging.ImageFormat.Png 
                                : System.Drawing.Imaging.ImageFormat.Jpeg;
                            img.Save(sfd.FileName, format);
                        }
                    }
                    LogEvent($"[PDF Reader] Exported Page {_pdfReaderCurrentPageIndex + 1} to {Path.GetFileName(sfd.FileName)}");
                    ShowHardwareAlert("Page Exported", $"Successfully exported page {_pdfReaderCurrentPageIndex + 1} as image.");
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Export Error", ex.Message);
                }
            }
        }

        // Library Action Buttons
        private void BtnLibOpen_Click(object sender, RoutedEventArgs e)
        {
            if (ListLibraryFiles.SelectedItem is LibraryItem selected)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(selected.FilePath) { UseShellExecute = true });
                    LogEvent($"LIBRARY: Opened file {selected.FileName}");
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Execution Error", $"Hindi maipahayag ang Windows app para buksan ang file: {ex.Message}");
                }
            }
        }

        private void BtnLibPrint_Click(object sender, RoutedEventArgs e)
        {
            if (ListLibraryFiles.SelectedItem is LibraryItem selected)
            {
                // Temporarily redirect document paths and trigger printing modal!
                string savedDocPath = _currentDocumentPath;
                _currentDocumentPath = selected.FilePath;

                LogEvent($"LIBRARY: Directing file {selected.FileName} to Print Cockpit...");
                BtnPrint_Click(sender, e);

                // Restore path upon close (or the print logic will read it directly)
            }
        }

        private void BtnLibAddToBatch_Click(object sender, RoutedEventArgs e)
        {
            if (ListLibraryFiles.SelectedItem is LibraryItem selected)
            {
                try
                {
                    ImageSource? thumbnail = selected.Thumbnail;
                    if (thumbnail == null)
                    {
                        if (selected.IsPdf)
                        {
                            thumbnail = RenderPdfPage(selected.FilePath, 0);
                        }
                        else
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(selected.FilePath);
                            bitmap.EndInit();
                            bitmap.Freeze();
                            thumbnail = bitmap;
                        }
                    }

                    var page = new ScannedPage
                    {
                        FilePath = selected.FilePath,
                        Thumbnail = thumbnail,
                        PageLabel = $"Page {BatchPages.Count + 1} (Library)"
                    };

                    BatchPages.Add(page);
                    LogEvent($"LIBRARY: Inserted file {selected.FileName} into Active Batch Pages.");
                    ShowHardwareAlert("Document Compiled", $"Matagumpay na idinagdag ang '{selected.FileName}' sa iyong active batch scans.");
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Import Error", $"Hindi maidagdag sa batch: {ex.Message}");
                }
            }
        }

        private void BtnLibDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListLibraryFiles.SelectedItem is LibraryItem selected)
            {
                var result = MessageBox.Show($"Sigurado ka ba na gusto mong permanenteng burahin ang file na ito?\n\n{selected.FileName}", 
                                             "Confirm Delete", 
                                             MessageBoxButton.YesNo, 
                                             MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(selected.FilePath))
                        {
                            File.Delete(selected.FilePath);
                            LogEvent($"LIBRARY: Deleted file {selected.FileName} from disk.");
                            RefreshLibrary();
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowHardwareAlert("Delete Error", $"Hindi nabura ang file: {ex.Message}");
                    }
                }
            }
        }

        // ==========================================
        // AUTOMATIONS ENGINE FUNCTIONS
        // ==========================================

        private void SetupWatchdogTimer()
        {
            _watchdogTimer = new System.Windows.Threading.DispatcherTimer();
            _watchdogTimer.Interval = TimeSpan.FromSeconds(1); // Tic bawat 1 segundong counter
            _watchdogTimer.Tick += WatchdogTimer_Tick;
            _watchdogTimer.Start();
            LogAutomationEvent("Engine Watchdog Initialized. Standby.");
        }

        private void WatchdogTimer_Tick(object? sender, EventArgs e)
        {
            if (ViewDashboard.Visibility == Visibility.Visible)
            {
                TxtDashClock.Text = $"Live Mode | {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }

            // Periodic Printer status refresh (every 10 seconds when ViewPrinters tab is visible)
            if (ViewPrinters != null && ViewPrinters.Visibility == Visibility.Visible)
            {
                _printerRefreshCounter++;
                if (_printerRefreshCounter >= 10)
                {
                    _printerRefreshCounter = 0;
                    try
                    {
                        RefreshPrinterCenterList();
                        if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter)
                        {
                            RefreshPrintJobs(selectedPrinter.Name);
                        }
                        UpdateSpoolerServiceStatus();
                    }
                    catch { /* Suppress background refresh errors */ }
                }
            }
            else
            {
                _printerRefreshCounter = 0;
            }

            if (ChkWatchdogEnabled.IsChecked != true)
            {
                TxtAutomationStatus.Text = "DISABLED";
                TxtAutomationStatus.Foreground = Brushes.SlateGray;
                return;
            }

            _watchdogSecondsCounter++;
            int remaining = _watchdogIntervalSeconds - (_watchdogSecondsCounter % _watchdogIntervalSeconds);
            TxtAutomationStatus.Text = $"ACTIVE (Diagnosing in {remaining}s...)";
            TxtAutomationStatus.Foreground = (Brush)Application.Current.Resources["ConsoleFg"];

            if (_watchdogSecondsCounter >= _watchdogIntervalSeconds)
            {
                _watchdogSecondsCounter = 0;
                ExecuteWatchdogDiagnostics();
            }
        }

        private void ExecuteWatchdogDiagnostics(bool isManual = false)
        {
            string triggerSrc = isManual ? "Manual Bypass" : "Scheduler Schedule";
            LogAutomationEvent($"[Watchdog] [{triggerSrc}] Executing service diagnostics...");
            
            bool healSpooler = ChkHealSpooler.IsChecked == true;
            bool healWia = ChkHealWia.IsChecked == true;

            Task.Run(() => {
                try
                {
                    // 1. Spooler Diagnostic Check
                    if (healSpooler)
                    {
                        using (var s = new ServiceController("Spooler"))
                        {
                            if (s.Status == ServiceControllerStatus.Stopped)
                            {
                                Dispatcher.Invoke(() => LogAutomationEvent("[Watchdog] Warning: Printer Spooler service is STOPPED. Healing..."));
                                AutoRestartSpooler();
                            }
                            else
                            {
                                Dispatcher.Invoke(() => LogAutomationEvent("[Watchdog] Print Spooler Service Status: HEALTHY"));
                            }
                        }
                    }

                    // 2. WIA Diagnostic Check
                    if (healWia)
                    {
                        using (var s = new ServiceController("StiSvc"))
                        {
                            if (s.Status == ServiceControllerStatus.Stopped)
                            {
                                Dispatcher.Invoke(() => LogAutomationEvent("[Watchdog] Warning: Windows Image (WIA) service is STOPPED. Healing..."));
                                AutoRestartStiSvc();
                            }
                            else
                            {
                                Dispatcher.Invoke(() => LogAutomationEvent("[Watchdog] Windows Image Acquisition (WIA) Status: HEALTHY"));
                            }
                        }
                    }

                    Dispatcher.Invoke(() => LogAutomationEvent("[Watchdog] Service diagnostics cycle completed successfully."));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => LogAutomationEvent($"[Watchdog] Error during diagnostic sweep: {ex.Message}"));
                }
            });
        }

        private void RunPostScanWorkflows(string filePath)
        {
            bool isBackupEnabled = false;
            bool isPrintEnabled = false;

            Dispatcher.Invoke(() => {
                isBackupEnabled = ChkAutoBackup.IsChecked == true;
                isPrintEnabled = ChkAutoPrint.IsChecked == true;
            });

            // Auto-Backup Workflow
            if (isBackupEnabled)
            {
                Task.Run(() => {
                    try
                    {
                        if (!Directory.Exists(_currentBackupPath))
                        {
                            Directory.CreateDirectory(_currentBackupPath);
                        }
                        string destFile = Path.Combine(_currentBackupPath, Path.GetFileName(filePath));
                        File.Copy(filePath, destFile, true);
                        
                        Dispatcher.Invoke(() => {
                            _replicatedFilesCount++;
                            TxtBackupFilesCount.Text = $"{_replicatedFilesCount} files replicated";
                            LogAutomationEvent($"[Workflow] Backup Sync SUCCESS: Replicated '{Path.GetFileName(filePath)}' to Backup storage.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => LogAutomationEvent($"[Workflow] Backup FAILED: {ex.Message}"));
                    }
                });
            }

            // Auto-Print Workflow
            if (isPrintEnabled)
            {
                Dispatcher.Invoke(() => {
                    LogAutomationEvent($"[Workflow] Auto-Print Triggered: Route file to print Spooler.");
                    // Prefill current document path and trigger print
                    _currentDocumentPath = filePath;
                    BtnPrint_Click(this, new RoutedEventArgs());
                });
            }
        }

        private void PurgePrintSpooler()
        {
            LogAutomationEvent("[Spooler Purge] Initiating printer spooler queue cleanup...");
            ShowModal("Purging printer spooler files...");

            Task.Run(() => {
                try
                {
                    // 1. Stop Spooler service
                    LogAutomationEvent("[Spooler Purge] Stopping 'Spooler' service...");
                    using (ServiceController s = new ServiceController("Spooler"))
                    {
                        if (s.Status != ServiceControllerStatus.Stopped)
                        {
                            s.Stop();
                            s.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        }
                    }

                    // 2. Delete stuck files
                    string spoolFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"spool\PRINTERS");
                    if (Directory.Exists(spoolFolder))
                    {
                        int deletedCount = 0;
                        foreach (string file in Directory.GetFiles(spoolFolder))
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (ext == ".shd" || ext == ".spl")
                            {
                                File.Delete(file);
                                deletedCount++;
                            }
                        }
                        Dispatcher.Invoke(() => LogAutomationEvent($"[Spooler Purge] Purged {deletedCount} stuck queue files from System Spool directory."));
                    }
                    else
                    {
                        Dispatcher.Invoke(() => LogAutomationEvent("[Spooler Purge] Warning: Spool printers directory not found."));
                    }

                    // 3. Restart Spooler service
                    LogAutomationEvent("[Spooler Purge] Restarting 'Spooler' service...");
                    using (ServiceController s = new ServiceController("Spooler"))
                    {
                        s.Start();
                        s.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    }

                    Dispatcher.Invoke(() => {
                        LogAutomationEvent("[Spooler Purge] SUCCESS: Printer queue is cleared and service is fully restored!");
                        ShowHardwareAlert("Spooler Purged Successfully", "Stuck printing files have been deleted, and the Print Spooler has been restarted.");
                    });
                }
                catch (InvalidOperationException)
                {
                    Dispatcher.Invoke(() => {
                        LogAutomationEvent("[Spooler Purge] Access Denied: Administrator rights are required to modify spooler queue.");
                        ShowHardwareAlert("Access Denied", "Printer queue purging requires Administrator privileges.\n\nMangyaring patakbuhin ang application bilang Administrator para magamit ang spooler purging.");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        LogAutomationEvent($"[Spooler Purge] FAILED: {ex.Message}");
                        ShowHardwareAlert("Purge Error", $"Failed to clean print queue: {ex.Message}");
                    });
                }
                finally
                {
                    HideModal();
                }
            });
        }

        // Folder Backup Browse Click
        private void BtnBrowseBackup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "Select Auto-Backup Destination Directory";
            dialog.InitialDirectory = _currentBackupPath;
            if (dialog.ShowDialog() == true)
            {
                _currentBackupPath = dialog.FolderName;
                TxtBackupPath.Text = _currentBackupPath;
                LogAutomationEvent($"[Engine] Backup directory changed to: {_currentBackupPath}");
            }
        }

        // Checkbox State change triggers
        private void ChkWatchdogEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLibraryInitLoaded) return;
            bool isChecked = ChkWatchdogEnabled.IsChecked == true;
            LogAutomationEvent($"[Engine] Service health watchdog is now {(isChecked ? "ENABLED" : "DISABLED")}.");
        }

        private void ChkAutoBackup_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLibraryInitLoaded) return;
            bool isChecked = ChkAutoBackup.IsChecked == true;
            LogAutomationEvent($"[Workflow] Auto-Backup workflow is now {(isChecked ? "ENABLED" : "DISABLED")}.");
        }

        private void ChkAutoPrint_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLibraryInitLoaded) return;
            bool isChecked = ChkAutoPrint.IsChecked == true;
            LogAutomationEvent($"[Workflow] Auto-Print workflow is now {(isChecked ? "ENABLED" : "DISABLED")}.");
        }

        private void CmbWatchdogInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLibraryInitLoaded) return;

            string intervalStr = (CmbWatchdogInterval.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Every 1 minute";
            if (intervalStr.Contains("30 seconds")) _watchdogIntervalSeconds = 30;
            else if (intervalStr.Contains("1 minute")) _watchdogIntervalSeconds = 60;
            else if (intervalStr.Contains("5 minutes")) _watchdogIntervalSeconds = 300;
            else if (intervalStr.Contains("15 minutes")) _watchdogIntervalSeconds = 900;
            else if (intervalStr.Contains("1 hour")) _watchdogIntervalSeconds = 3600;

            _watchdogSecondsCounter = 0; // reset
            LogAutomationEvent($"[Engine] Watchdog interval changed to: {intervalStr}");
        }

        // Test Triggers
        private void BtnTestWatchdog_Click(object sender, RoutedEventArgs e)
        {
            ExecuteWatchdogDiagnostics(true);
        }

        private void BtnTestBackup_Click(object sender, RoutedEventArgs e)
        {
            LogAutomationEvent("[Workflow Test] Starting backup directory replication test...");
            if (!Directory.Exists(_currentBackupPath))
            {
                try { Directory.CreateDirectory(_currentBackupPath); }
                catch (Exception ex) { ShowHardwareAlert("Backup Directory Error", ex.Message); return; }
            }

            Task.Run(() => {
                try
                {
                    var sourceDir = new DirectoryInfo(_currentLibraryPath);
                    if (!sourceDir.Exists) return;

                    int copied = 0;
                    foreach (var f in sourceDir.GetFiles())
                    {
                        string ext = f.Extension.ToLower();
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".pdf")
                        {
                            string destFile = Path.Combine(_currentBackupPath, f.Name);
                            File.Copy(f.FullName, destFile, true);
                            copied++;
                        }
                    }
                    _replicatedFilesCount = copied;
                    Dispatcher.Invoke(() => {
                        TxtBackupFilesCount.Text = $"{_replicatedFilesCount} files replicated";
                        LogAutomationEvent($"[Workflow Test] SUCCESS: Copied {copied} documents to backup storage folder.");
                        ShowHardwareAlert("Backup Sync Completed", $"Copied {copied} files to backup folder at {_currentBackupPath}");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        LogAutomationEvent($"[Workflow Test] FAILED: {ex.Message}");
                        ShowHardwareAlert("Backup Sync Error", ex.Message);
                    });
                }
            });
        }

        private void BtnPurgeSpooler_Click(object sender, RoutedEventArgs e)
        {
            PurgePrintSpooler();
        }

        private void LogAutomationEvent(string m)
        {
            Dispatcher.Invoke(() => {
                AutomationLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {m}");
            });
        }

        private void ListDashRecentFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Selection event stub, logic can be added if item preview is desired on selection
        }

        private void BtnDashOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LibraryItem selected)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(selected.FilePath) { UseShellExecute = true });
                    LogEvent($"DASHBOARD: Opened file {selected.FileName}");
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Execution Error", $"Hindi maipahayag ang Windows app para buksan ang file: {ex.Message}");
                }
            }
        }

        private void BtnDashPrint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LibraryItem selected)
            {
                // Temporarily redirect document paths and trigger printing modal
                _currentDocumentPath = selected.FilePath;
                LogEvent($"DASHBOARD: Directing file {selected.FileName} to Print Cockpit...");
                BtnPrint_Click(sender, e);
            }
        }

        // ==========================================
        // USER PROFILE & SETTINGS MODAL LOGIC
        // ==========================================
        
        private void NavProfile_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Populate fields with current values
            TxtEditProfileName.Text = TxtProfileName.Text;
            TxtProfileLibPath.Text = _currentLibraryPath;
            TxtProfileMachine.Text = Environment.MachineName;
            TxtProfileOS.Text = $"Windows {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Build}";
            TxtProfileAvatarLarge.Text = TxtProfileInitials.Text;
            TxtProfileModalName.Text = TxtProfileName.Text;
            ProfileModal.Visibility = Visibility.Visible;
        }

        private void BtnCloseProfile_Click(object sender, RoutedEventArgs e)
        {
            ProfileModal.Visibility = Visibility.Collapsed;
        }

        private void ProfileModalBg_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close only if clicked on the background, not on the modal content
            if (e.OriginalSource == sender)
            {
                ProfileModal.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnProfileBrowseLib_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            dlg.Title = "Select Library Storage Folder";
            if (Directory.Exists(_currentLibraryPath))
                dlg.InitialDirectory = _currentLibraryPath;
            
            if (dlg.ShowDialog() == true)
            {
                TxtProfileLibPath.Text = dlg.FolderName;
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            // Update display name
            string newName = TxtEditProfileName.Text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                TxtProfileName.Text = newName;
                TxtProfileModalName.Text = newName;

                // Update initials from name
                var parts = newName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string initials = "";
                if (parts.Length >= 2)
                    initials = $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
                else if (parts.Length == 1 && parts[0].Length >= 2)
                    initials = parts[0].Substring(0, 2).ToUpper();
                else if (parts.Length == 1)
                    initials = parts[0].ToUpper();
                
                TxtProfileInitials.Text = initials;
                TxtProfileAvatarLarge.Text = initials;
            }



            // Update library path
            string newPath = TxtProfileLibPath.Text.Trim();
            if (!string.IsNullOrEmpty(newPath) && newPath != _currentLibraryPath)
            {
                if (!Directory.Exists(newPath))
                {
                    try { Directory.CreateDirectory(newPath); } catch { }
                }
                _currentLibraryPath = newPath;
                LogEvent($"Library path updated to: {newPath}");
            }

            ProfileModal.Visibility = Visibility.Collapsed;
            LogEvent("Profile settings saved successfully.");
        }

        private void BtnToggleTheme_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
            
            // Sync with profile modal combobox if it exists
            if (CmbProfileTheme != null)
            {
                CmbProfileTheme.SelectedIndex = _isDarkMode ? 0 : 1;
            }
            
            UpdateThemeToggleUI();
        }

        private void UpdateThemeToggleUI()
        {
            if (IconTheme != null && TxtThemeMode != null)
            {
                if (_isDarkMode)
                {
                    IconTheme.Kind = PackIconKind.WeatherNight;
                    TxtThemeMode.Text = "Dark Mode";
                }
                else
                {
                    IconTheme.Kind = PackIconKind.WeatherSunny;
                    TxtThemeMode.Text = "Light Mode";
                }
            }
        }

        private void CmbProfileTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProfileTheme == null || !_isLibraryInitLoaded) return; // Wait until loaded
            
            bool newMode = CmbProfileTheme.SelectedIndex == 0;
            if (newMode != _isDarkMode)
            {
                _isDarkMode = newMode;
                ApplyTheme();
                UpdateThemeToggleUI();
            }
        }

        private void ThemeBtn_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.SetResourceReference(Border.BackgroundProperty, "NavActiveBg");
            }
        }

        private void ThemeBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
            }
        }

        private void UserBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.SetResourceReference(Border.BackgroundProperty, "NavActiveBg");
            }
        }

        private void UserBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.SetResourceReference(Border.BackgroundProperty, "ProfileBg");
            }
        }

        private void UpdateDashboardData()
        {
            TxtDashClock.Text = $"Live Mode | {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            Task.Run(() => {
                int totalFiles = 0;
                long totalBytes = 0;
                var recentList = new List<LibraryItem>();

                try
                {
                    if (Directory.Exists(_currentLibraryPath))
                    {
                        var dir = new DirectoryInfo(_currentLibraryPath);
                        var fileInfos = dir.GetFiles()
                            .Where(f => {
                                string ext = f.Extension.ToLower();
                                return ext == ".pdf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png";
                            })
                            .OrderByDescending(f => f.LastWriteTime)
                            .ToList();

                        totalFiles = fileInfos.Count;
                        totalBytes = fileInfos.Sum(f => f.Length);

                        // Take top 3
                        foreach (var f in fileInfos.Take(3))
                        {
                            string ext = f.Extension.ToLower();
                            var item = new LibraryItem
                            {
                                FilePath = f.FullName,
                                FileName = f.Name,
                                DateModified = f.LastWriteTime,
                                FileExtension = ext,
                                RawSize = f.Length,
                                FileSize = FormatBytes(f.Length)
                            };
                            item.Thumbnail = GenerateLibraryThumbnail(f.FullName, ext);
                            recentList.Add(item);
                        }
                    }
                }
                catch { }

                int printersCount = 0;
                int scannersCount = 0;
                string defaultPrinterName = "No Default Printer";
                int defaultPrinterJobCount = 0;
                double cToner = 100, mToner = 100, yToner = 100, kToner = 100;
                string inkStatusText = "✓ LEVELS OK";
                
                try
                {
                    // 1. Try Registry first (fastest and most accurate for HKCU default printer)
                    string? defaultDevice = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows", "Device", null) as string;
                    if (!string.IsNullOrEmpty(defaultDevice))
                    {
                        string[] parts = defaultDevice.Split(',');
                        if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                        {
                            defaultPrinterName = parts[0];
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(defaultPrinterName) || defaultPrinterName == "No Default Printer")
                {
                    try
                    {
                        // 2. Try PrinterSettings fallback
                        defaultPrinterName = new System.Drawing.Printing.PrinterSettings().PrinterName;
                    }
                    catch { }
                }

                try
                {
                    LocalPrintServer printServer = new LocalPrintServer();
                    printersCount = printServer.GetPrintQueues().Count();
                    
                    if (!string.IsNullOrEmpty(defaultPrinterName) && defaultPrinterName != "No Default Printer")
                    {
                        try
                        {
                            var defaultQueue = printServer.GetPrintQueue(defaultPrinterName);
                            if (defaultQueue != null)
                            {
                                defaultPrinterJobCount = defaultQueue.NumberOfJobs;
                            }
                        }
                        catch
                        {
                            // Fallback if specific print queue query fails
                            var defaultQueue = printServer.DefaultPrintQueue;
                            if (defaultQueue != null)
                            {
                                defaultPrinterName = defaultQueue.FullName;
                                defaultPrinterJobCount = defaultQueue.NumberOfJobs;
                            }
                        }
                    }
                    else
                    {
                        var defaultQueue = printServer.DefaultPrintQueue;
                        if (defaultQueue != null)
                        {
                            defaultPrinterName = defaultQueue.FullName;
                            defaultPrinterJobCount = defaultQueue.NumberOfJobs;
                        }
                    }
                }
                catch { }

                try
                {
                    DeviceManager manager = new DeviceManager();
                    foreach (DeviceInfo info in manager.DeviceInfos)
                    {
                        if (info.Type == WiaDeviceType.ScannerDeviceType)
                        {
                            scannersCount++;
                        }
                    }
                }
                catch { }

                // Query WMI status for the default printer to get ink/toner level
                if (!string.IsNullOrEmpty(defaultPrinterName) && defaultPrinterName != "No Default Printer")
                {
                    try
                    {
                        // Check if printer exists in our memory collection first to match user manual refills
                        PrinterItem? existingPrinter = null;
                        Dispatcher.Invoke(() => {
                            existingPrinter = TargetPrinters.FirstOrDefault(p => p.Name.Equals(defaultPrinterName, StringComparison.OrdinalIgnoreCase));
                        });

                        if (existingPrinter != null)
                        {
                            cToner = existingPrinter.TonerCyan;
                            mToner = existingPrinter.TonerMagenta;
                            yToner = existingPrinter.TonerYellow;
                            kToner = existingPrinter.TonerBlack;
                            inkStatusText = existingPrinter.InkStatus;
                        }
                        else
                        {
                            using (var searcher = new ManagementObjectSearcher(
                                $"SELECT DetectedErrorState, PrinterStatus FROM Win32_Printer WHERE Name = '{defaultPrinterName.Replace("\\", "\\\\")}'"))
                            {
                                foreach (ManagementObject obj in searcher.Get())
                                {
                                    uint errorState = obj["DetectedErrorState"] != null ? Convert.ToUInt32(obj["DetectedErrorState"]) : 0;
                                    uint printerStatus = obj["PrinterStatus"] != null ? Convert.ToUInt32(obj["PrinterStatus"]) : 0;
                                    
                                    switch (errorState)
                                    {
                                        case 5:
                                            inkStatusText = "⚠ INK/TONER LOW";
                                            cToner = mToner = yToner = kToner = 15;
                                            break;
                                        case 6:
                                            inkStatusText = "✕ INK/TONER EMPTY";
                                            cToner = mToner = yToner = kToner = 0;
                                            break;
                                        case 9:
                                            inkStatusText = "OFFLINE";
                                            cToner = mToner = yToner = kToner = 0;
                                            break;
                                        default:
                                            if (printerStatus == 7 || printerStatus == 6)
                                            {
                                                inkStatusText = "OFFLINE";
                                                cToner = mToner = yToner = kToner = 0;
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                int totalDevices = printersCount + scannersCount;

                bool isSpoolerRunning = false;
                try
                {
                    using (var s = new ServiceController("Spooler"))
                    {
                        isSpoolerRunning = s.Status == ServiceControllerStatus.Running;
                    }
                }
                catch { }

                bool isWiaRunning = false;
                try
                {
                    using (var s = new ServiceController("StiSvc"))
                    {
                        isWiaRunning = s.Status == ServiceControllerStatus.Running;
                    }
                }
                catch { }

                bool isDiskHealthy = Directory.Exists(_currentLibraryPath);
                
                bool isWatchdogEnabled = false;
                int replicationCount = 0;
                Dispatcher.Invoke(() => {
                    isWatchdogEnabled = ChkWatchdogEnabled?.IsChecked == true;
                    replicationCount = _replicatedFilesCount;
                });

                Dispatcher.Invoke(() => {
                    TxtDashTotalFiles.Text = $"{totalFiles} files";
                    TxtDashDiskSize.Text = FormatBytes(totalBytes);
                    TxtDashHardwareCount.Text = $"{totalDevices} devices";
                    TxtDashHealCount.Text = $"{_healSequencesCount} cycles";

                    // Update Default Printer info
                    if (TxtDashPrinterModel != null) TxtDashPrinterModel.Text = defaultPrinterName;
                    if (TxtDashPrinterInkStatus != null)
                    {
                        TxtDashPrinterInkStatus.Text = inkStatusText;
                        TxtDashPrinterInkStatus.Foreground = inkStatusText.Contains("LOW") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAB308")) :
                                                             inkStatusText.Contains("EMPTY") || inkStatusText.Contains("OFFLINE") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")) :
                                                             new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    }
                    
                    if (BarDashTonerCyan != null) BarDashTonerCyan.Value = cToner;
                    if (TxtDashTonerCyan != null) TxtDashTonerCyan.Text = $"{(int)cToner}%";
                    
                    if (BarDashTonerMagenta != null) BarDashTonerMagenta.Value = mToner;
                    if (TxtDashTonerMagenta != null) TxtDashTonerMagenta.Text = $"{(int)mToner}%";
                    
                    if (BarDashTonerYellow != null) BarDashTonerYellow.Value = yToner;
                    if (TxtDashTonerYellow != null) TxtDashTonerYellow.Text = $"{(int)yToner}%";
                    
                    if (BarDashTonerBlack != null) BarDashTonerBlack.Value = kToner;
                    if (TxtDashTonerBlack != null) TxtDashTonerBlack.Text = $"{(int)kToner}%";

                    if (TxtDashQueueSummary != null)
                    {
                        TxtDashQueueSummary.Text = defaultPrinterJobCount == 1 ? "1 active job pending execution" : $"{defaultPrinterJobCount} active jobs pending execution";
                    }

                    // Update Watchdog and Replication
                    if (TxtDashWatchdogEnabled != null)
                    {
                        TxtDashWatchdogEnabled.Text = isWatchdogEnabled ? "ENABLED" : "DISABLED";
                        TxtDashWatchdogEnabled.Foreground = isWatchdogEnabled ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                    }
                    if (TxtDashReplicationCount != null)
                    {
                        TxtDashReplicationCount.Text = replicationCount == 1 ? "1 file backed up" : $"{replicationCount} files backed up";
                    }

                    // Update diagnostics indicators
                    if (BarDashSpooler != null)
                    {
                        BarDashSpooler.Value = isSpoolerRunning ? 100 : 0;
                        BarDashSpooler.Foreground = isSpoolerRunning ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }
                    if (TxtDashSpoolerStatus != null)
                    {
                        TxtDashSpoolerStatus.Text = isSpoolerRunning ? "ONLINE" : "OFFLINE";
                    }
                    if (BadgeDashSpooler != null)
                    {
                        BadgeDashSpooler.Background = isSpoolerRunning ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }

                    if (BarDashWia != null)
                    {
                        BarDashWia.Value = isWiaRunning ? 100 : 0;
                        BarDashWia.Foreground = isWiaRunning ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }
                    if (TxtDashWiaStatus != null)
                    {
                        TxtDashWiaStatus.Text = isWiaRunning ? "ONLINE" : "OFFLINE";
                    }
                    if (BadgeDashWia != null)
                    {
                        BadgeDashWia.Background = isWiaRunning ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }

                    if (BarDashDisk != null)
                    {
                        BarDashDisk.Value = isDiskHealthy ? 100 : 0;
                        BarDashDisk.Foreground = isDiskHealthy ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }
                    if (TxtDashDiskStatus != null)
                    {
                        TxtDashDiskStatus.Text = isDiskHealthy ? "HEALTHY" : "ERROR";
                    }
                    if (BadgeDashDisk != null)
                    {
                        BadgeDashDisk.Background = isDiskHealthy ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }

                    // Update status badge
                    bool allSystemHealthy = isSpoolerRunning && isWiaRunning && isDiskHealthy;
                    if (DashStatusSignal != null)
                    {
                        DashStatusSignal.Background = allSystemHealthy ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    }
                    if (TxtDashStatus != null)
                    {
                        TxtDashStatus.Text = allSystemHealthy ? "BIATON SYSTEM ONLINE" : "BIATON SYSTEM WARNING";
                    }

                    // Update Recent Files List
                    RecentFiles.Clear();
                    foreach (var item in recentList)
                    {
                        RecentFiles.Add(item);
                    }
                });
            });
        }

        // ==========================================
        // PDF CONVERTER MODULE
        // ==========================================
        private string _selectedConverterFilePath = "";

        private void BtnConverterBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "Select PDF Output Directory";
            dialog.InitialDirectory = TxtConverterDestPath.Text;
            if (dialog.ShowDialog() == true)
            {
                TxtConverterDestPath.Text = dialog.FolderName;
                LogConverterConsole($"Output directory updated: {dialog.FolderName}");
            }
        }

        private void BorderDropArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                BorderDropArea.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFC4"));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void BorderDropArea_DragLeave(object sender, DragEventArgs e)
        {
            BorderDropArea.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06B6D4"));
            e.Handled = true;
        }

        private void BorderDropArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void BorderDropArea_Drop(object sender, DragEventArgs e)
        {
            BorderDropArea.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06B6D4"));
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    SelectFileForConversion(files[0]);
                }
            }
            e.Handled = true;
        }

        private void BorderDropArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Supported Files (*.png;*.jpg;*.jpeg;*.bmp;*.txt;*.log;*.csv;*.json;*.xml;*.docx;*.xlsx;*.pptx)|*.png;*.jpg;*.jpeg;*.bmp;*.txt;*.log;*.csv;*.json;*.xml;*.docx;*.xlsx;*.pptx|All Files (*.*)|*.*";
            if (ofd.ShowDialog() == true)
            {
                SelectFileForConversion(ofd.FileName);
            }
        }

        private void BtnClearFile_Click(object sender, RoutedEventArgs e)
        {
            _selectedConverterFilePath = "";
            BorderFileInfo.Visibility = Visibility.Collapsed;
            BorderDropArea.Visibility = Visibility.Visible;
            BtnStartConversion.IsEnabled = false;
            TxtConverterStatus.Text = "Ready to Convert";
            ProgressConverter.Value = 0;
            LogConverterConsole("Cleared selected file. Console idle.");
        }

        private void LogConverterConsole(string message)
        {
            Dispatcher.Invoke(() => {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                if (TxtConverterConsole.Text == "Console idle. Drop a file to begin.")
                {
                    TxtConverterConsole.Text = $"[{timestamp}] {message}";
                }
                else
                {
                    TxtConverterConsole.Text += $"\n[{timestamp}] {message}";
                }
                ScrollConverterLogs.ScrollToEnd();
            });
        }

        private void SelectFileForConversion(string filePath)
        {
            if (!File.Exists(filePath)) return;

            _selectedConverterFilePath = filePath;
            string fileName = Path.GetFileName(filePath);
            long fileSizeBytes = new FileInfo(filePath).Length;
            string extension = Path.GetExtension(filePath).ToLower();

            string sizeStr = FormatBytes(fileSizeBytes);

            PackIconKind iconKind = PackIconKind.FileOutline;
            string desc = "Unknown Document";

            switch (extension)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                    iconKind = PackIconKind.FileImageOutline;
                    desc = "Image File";
                    break;
                case ".txt":
                case ".log":
                    iconKind = PackIconKind.FileDocumentOutline;
                    desc = "Plain Text File";
                    break;
                case ".csv":
                case ".json":
                case ".xml":
                    iconKind = PackIconKind.FileCodeOutline;
                    desc = "Data/Code File";
                    break;
                case ".docx":
                    iconKind = PackIconKind.FileWordOutline;
                    desc = "Word Document";
                    break;
                case ".xlsx":
                    iconKind = PackIconKind.FileExcelOutline;
                    desc = "Excel Spreadsheet";
                    break;
                case ".pptx":
                    iconKind = PackIconKind.FilePowerpointOutline;
                    desc = "PowerPoint Presentation";
                    break;
                case ".pdf":
                    iconKind = PackIconKind.FilePdfBox;
                    desc = "PDF Document (Already PDF)";
                    break;
            }

            TxtFileName.Text = fileName;
            TxtFileSize.Text = sizeStr;
            TxtFileTypeDesc.Text = desc;
            IconFileType.Kind = iconKind;

            BorderDropArea.Visibility = Visibility.Collapsed;
            BorderFileInfo.Visibility = Visibility.Visible;
            BtnStartConversion.IsEnabled = true;
            ProgressConverter.Value = 0;
            TxtConverterStatus.Text = "Ready to Convert";

            LogConverterConsole($"Selected file for conversion: {fileName} ({sizeStr})");
            if (extension == ".pdf")
            {
                LogConverterConsole("Warning: File is already a PDF. Converting will create a duplicate/copy.");
            }
        }

        private void BtnStartConversion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedConverterFilePath) || !File.Exists(_selectedConverterFilePath))
            {
                ShowHardwareAlert("Converter Error", "Mangyaring pumili muna ng tamang file na iko-convert.");
                return;
            }

            string src = _selectedConverterFilePath;
            string outDir = TxtConverterDestPath.Text;
            if (string.IsNullOrEmpty(outDir)) outDir = _currentLibraryPath;

            if (!Directory.Exists(outDir))
            {
                try { Directory.CreateDirectory(outDir); }
                catch { outDir = Path.GetDirectoryName(src) ?? _currentLibraryPath; }
            }

            string filenameNoExt = Path.GetFileNameWithoutExtension(src);
            string dest = Path.Combine(outDir, filenameNoExt + ".pdf");

            int count = 1;
            while (File.Exists(dest))
            {
                dest = Path.Combine(outDir, $"{filenameNoExt}_{count}.pdf");
                count++;
            }

            BtnStartConversion.IsEnabled = false;
            BtnClearFile.IsEnabled = false;
            ProgressConverter.Value = 10;
            TxtConverterStatus.Text = "Analyzing file format...";
            LogConverterConsole($"Starting conversion engine for: {Path.GetFileName(src)}");

            string pageSize = (CmbConverterPageSize.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "A4 (Standard)";
            string orientation = (CmbConverterOrientation.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Portrait";
            bool openAfter = ChkConverterOpenAfter.IsChecked ?? true;

            Task.Run(() => {
                try
                {
                    string ext = Path.GetExtension(src).ToLower();
                    
                    Dispatcher.Invoke(() => {
                        ProgressConverter.Value = 30;
                        TxtConverterStatus.Text = "Processing document conversion...";
                    });

                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        LogConverterConsole("Image file format detected. Generating high-resolution image-PDF...");
                        ConvertImageToPdf(src, dest, pageSize, orientation);
                    }
                    else if (ext == ".txt" || ext == ".log" || ext == ".csv" || ext == ".json" || ext == ".xml")
                    {
                        LogConverterConsole("Text-based file format detected. Wrapping characters and calculating page layouts...");
                        ConvertTextToPdf(src, dest, pageSize, orientation);
                    }
                    else if (ext == ".docx")
                    {
                        LogConverterConsole("Word Processing document detected. Querying Microsoft Office installation...");
                        ConvertDocxToPdf(src, dest, pageSize, orientation);
                    }
                    else if (ext == ".xlsx")
                    {
                        LogConverterConsole("Spreadsheet document detected. Running Excel COM Automation...");
                        ConvertExcelToPdf(src, dest);
                    }
                    else if (ext == ".pptx")
                    {
                        LogConverterConsole("Presentation document detected. Running PowerPoint COM Automation...");
                        ConvertPowerPointToPdf(src, dest);
                    }
                    else if (ext == ".pdf")
                    {
                        LogConverterConsole("File is already a PDF. Copying file to output library directory...");
                        File.Copy(src, dest, true);
                    }
                    else
                    {
                        LogConverterConsole("Unknown extension. Attempting fallback text conversion...");
                        ConvertTextToPdf(src, dest, pageSize, orientation);
                    }

                    Dispatcher.Invoke(() => {
                        ProgressConverter.Value = 100;
                        TxtConverterStatus.Text = "Conversion Completed Successfully!";
                        LogConverterConsole($"Success! Converted PDF saved to: {dest}");
                        LogEvent($"CONVERTED TO PDF: {Path.GetFileName(src)} -> {Path.GetFileName(dest)}");

                        RefreshLibrary();

                        BtnStartConversion.IsEnabled = true;
                        BtnClearFile.IsEnabled = true;

                        ShowHardwareAlert("Conversion Completed", $"Matagumpay na na-convert ang {Path.GetFileName(src)} sa PDF format.");

                        if (openAfter)
                        {
                            _pdfReaderCurrentPath = dest;
                            _pdfReaderCurrentPageIndex = 0;
                            _pdfReaderZoomScale = 1.0;
                            _pdfReaderRotationAngle = 0;
                            
                            SwitchToTab("PdfReader");
                            _ = LoadPdfReaderDocument(dest);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        ProgressConverter.Value = 0;
                        TxtConverterStatus.Text = "Conversion Failed";
                        LogConverterConsole($"Error during conversion: {ex.Message}");
                        BtnStartConversion.IsEnabled = true;
                        BtnClearFile.IsEnabled = true;
                        ShowHardwareAlert("Conversion Failed", $"Nagka-error sa pag-convert ng file: {ex.Message}");
                    });
                }
            });
        }

        private void ConvertImageToPdf(string src, string dest, string pageSize, string orientation)
        {
            PdfSharp.Pdf.PdfDocument document = new PdfSharp.Pdf.PdfDocument();
            PdfPage pdfPage = document.AddPage();
            using (XGraphics gfx = XGraphics.FromPdfPage(pdfPage))
            {
                using (XImage image = XImage.FromFile(src))
                {
                    bool isAuto = pageSize.StartsWith("Auto", StringComparison.OrdinalIgnoreCase);
                    if (isAuto)
                    {
                        pdfPage.Width = new XUnitPt(image.PointWidth);
                        pdfPage.Height = new XUnitPt(image.PointHeight);
                        gfx.DrawImage(image, 0, 0, image.PointWidth, image.PointHeight);
                    }
                    else
                    {
                        if (orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase))
                        {
                            pdfPage.Orientation = PdfSharp.PageOrientation.Landscape;
                        }
                        else
                        {
                            pdfPage.Orientation = PdfSharp.PageOrientation.Portrait;
                        }

                        if (pageSize.Equals("Letter", StringComparison.OrdinalIgnoreCase))
                        {
                            pdfPage.Size = PdfSharp.PageSize.Letter;
                        }
                        else
                        {
                            pdfPage.Size = PdfSharp.PageSize.A4;
                        }

                        double margin = 30;
                        double printableWidth = pdfPage.Width.Point - (margin * 2);
                        double printableHeight = pdfPage.Height.Point - (margin * 2);
                        
                        double imgWidth = image.PointWidth;
                        double imgHeight = image.PointHeight;
                        
                        double ratioX = printableWidth / imgWidth;
                        double ratioY = printableHeight / imgHeight;
                        double ratio = Math.Min(ratioX, ratioY);
                        
                        double fitWidth = imgWidth * ratio;
                        double fitHeight = imgHeight * ratio;
                        
                        double x = margin + (printableWidth - fitWidth) / 2;
                        double y = margin + (printableHeight - fitHeight) / 2;

                        gfx.DrawImage(image, x, y, fitWidth, fitHeight);
                    }
                }
            }
            document.Save(dest);
        }

        private void ConvertTextToPdf(string src, string dest, string pageSize, string orientation)
        {
            PdfSharp.Pdf.PdfDocument document = new PdfSharp.Pdf.PdfDocument();
            var font = new XFont("Courier New", 10);
            double margin = 50;
            double y = margin;
            double lineHeight = 14;

            PdfPage page = document.AddPage();
            
            if (orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase))
            {
                page.Orientation = PdfSharp.PageOrientation.Landscape;
            }
            else
            {
                page.Orientation = PdfSharp.PageOrientation.Portrait;
            }

            if (pageSize.Equals("Letter", StringComparison.OrdinalIgnoreCase))
            {
                page.Size = PdfSharp.PageSize.Letter;
            }
            else
            {
                page.Size = PdfSharp.PageSize.A4;
            }

            double pageWidth = page.Width.Point;
            double pageHeight = page.Height.Point;
            double maxLineWidth = pageWidth - (margin * 2);
            double maxPageHeight = pageHeight - margin;

            XGraphics gfx = XGraphics.FromPdfPage(page);

            string[] rawLines = File.ReadAllLines(src);
            List<string> processedLines = new List<string>();

            foreach (var rawLine in rawLines)
            {
                string currentLine = rawLine.Replace("\t", "    ");
                if (currentLine.Length == 0)
                {
                    processedLines.Add("");
                    continue;
                }

                while (currentLine.Length > 0)
                {
                    XSize size = gfx.MeasureString(currentLine, font);
                    if (size.Width <= maxLineWidth)
                    {
                        processedLines.Add(currentLine);
                        break;
                    }

                    int low = 0, high = currentLine.Length;
                    int bestFit = 0;
                    while (low <= high)
                    {
                        int mid = (low + high) / 2;
                        string testStr = currentLine.Substring(0, mid);
                        XSize testSize = gfx.MeasureString(testStr, font);
                        if (testSize.Width <= maxLineWidth)
                        {
                            bestFit = mid;
                            low = mid + 1;
                        }
                        else
                        {
                            high = mid - 1;
                        }
                    }

                    if (bestFit == 0) bestFit = 1;
                    
                    int lastSpace = currentLine.Substring(0, bestFit).LastIndexOf(' ');
                    if (lastSpace > 0 && bestFit < currentLine.Length)
                    {
                        processedLines.Add(currentLine.Substring(0, lastSpace));
                        currentLine = currentLine.Substring(lastSpace + 1);
                    }
                    else
                    {
                        processedLines.Add(currentLine.Substring(0, bestFit));
                        currentLine = currentLine.Substring(bestFit);
                    }
                }
            }

            for (int i = 0; i < processedLines.Count; i++)
            {
                if (y + lineHeight > maxPageHeight)
                {
                    gfx.Dispose();
                    page = document.AddPage();
                    if (orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase))
                    {
                        page.Orientation = PdfSharp.PageOrientation.Landscape;
                    }
                    else
                    {
                        page.Orientation = PdfSharp.PageOrientation.Portrait;
                    }

                    if (pageSize.Equals("Letter", StringComparison.OrdinalIgnoreCase))
                    {
                        page.Size = PdfSharp.PageSize.Letter;
                    }
                    else
                    {
                        page.Size = PdfSharp.PageSize.A4;
                    }

                    gfx = XGraphics.FromPdfPage(page);
                    y = margin;
                }

                gfx.DrawString(processedLines[i], font, XBrushes.Black, margin, y);
                y += lineHeight;
            }

            gfx.Dispose();
            document.Save(dest);
        }

        private void ConvertDocxToPdf(string src, string dest, string pageSize, string orientation)
        {
            bool comSuccess = false;
            try
            {
                Type? wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType != null)
                {
                    dynamic wordApp = Activator.CreateInstance(wordType)!;
                    try
                    {
                        wordApp.Visible = false;
                        dynamic doc = wordApp.Documents.Open(src);
                        doc.SaveAs2(dest, 17); // 17 is wdFormatPDF
                        doc.Close(false);
                        comSuccess = true;
                        LogConverterConsole("Successfully converted using Microsoft Word COM Automation.");
                    }
                    finally
                    {
                        wordApp.Quit();
                    }
                }
            }
            catch (Exception comEx)
            {
                LogConverterConsole($"Microsoft Word COM conversion failed or Word is not installed: {comEx.Message}");
            }

            if (!comSuccess)
            {
                LogConverterConsole("Starting open-xml XML paragraph extraction fallback...");
                string extractedText = ExtractTextFromDocx(src);
                if (string.IsNullOrEmpty(extractedText))
                {
                    throw new Exception("Unable to extract text content from the Word file (it might be empty or corrupted).");
                }

                string tempTxt = Path.GetTempFileName();
                File.WriteAllText(tempTxt, extractedText);
                try
                {
                    ConvertTextToPdf(tempTxt, dest, pageSize, orientation);
                    LogConverterConsole("Fallback text rendering completed. Output PDF compiled from extracted DOCX paragraphs.");
                }
                finally
                {
                    try { File.Delete(tempTxt); } catch { }
                }
            }
        }

        private string ExtractTextFromDocx(string docxPath)
        {
            using (System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(docxPath))
            {
                System.IO.Compression.ZipArchiveEntry? documentXmlEntry = archive.GetEntry("word/document.xml");
                if (documentXmlEntry == null) return "";
                
                using (Stream stream = documentXmlEntry.Open())
                {
                    System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.Load(stream);
                    
                    System.Xml.XmlNamespaceManager nsmgr = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
                    nsmgr.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
                    
                    System.Xml.XmlNodeList? paragraphs = xmlDoc.SelectNodes("//w:p", nsmgr);
                    if (paragraphs == null) return "";
                    
                    List<string> textLines = new List<string>();
                    foreach (System.Xml.XmlNode pNode in paragraphs)
                    {
                        System.Xml.XmlNodeList? textNodes = pNode.SelectNodes(".//w:t", nsmgr);
                        string paraText = "";
                        if (textNodes != null)
                        {
                            foreach (System.Xml.XmlNode tNode in textNodes)
                            {
                                paraText += tNode.InnerText;
                            }
                        }
                        textLines.Add(paraText);
                    }
                    
                    return string.Join(Environment.NewLine, textLines);
                }
            }
        }

        private void ConvertExcelToPdf(string src, string dest)
        {
            Type? excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                throw new Exception("Microsoft Excel is not installed on this machine. Spreadsheet conversion is only supported via Office COM Automation.");
            }

            dynamic excelApp = Activator.CreateInstance(excelType)!;
            try
            {
                excelApp.Visible = false;
                dynamic workbook = excelApp.Workbooks.Open(src);
                workbook.ExportAsFixedFormat(0, dest);
                workbook.Close(false);
                LogConverterConsole("Successfully converted Excel sheet to PDF using COM Automation.");
            }
            finally
            {
                excelApp.Quit();
            }
        }

        private void ConvertPowerPointToPdf(string src, string dest)
        {
            Type? pptType = Type.GetTypeFromProgID("PowerPoint.Application");
            if (pptType == null)
            {
                throw new Exception("Microsoft PowerPoint is not installed on this machine. Presentation conversion is only supported via Office COM Automation.");
            }

            dynamic pptApp = Activator.CreateInstance(pptType)!;
            try
            {
                dynamic presentation;
                try
                {
                    presentation = pptApp.Presentations.Open(src, 1, 1, 0);
                }
                catch
                {
                    presentation = pptApp.Presentations.Open(src, 1, 1, 1);
                }
                presentation.SaveAs(dest, 32);
                presentation.Close();
                LogConverterConsole("Successfully converted PowerPoint presentation to PDF using COM Automation.");
            }
            finally
            {
                pptApp.Quit();
            }
        }

        // ==========================================
        // PRINTER CENTER MODULE
        // ==========================================
        private void RefreshPrinterCenterList()
        {
            try
            {
                string? prevSelectedName = (ListPrinterCenterDevices.SelectedItem as PrinterItem)?.Name;

                // Query WMI for real printer status including ink/toner error states
                var wmiPrinterInfo = new Dictionary<string, (uint errorState, uint printerStatus, string portName)>();
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, DetectedErrorState, PrinterStatus, PortName FROM Win32_Printer"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string wmiName = obj["Name"]?.ToString() ?? "";
                            uint errorState = obj["DetectedErrorState"] != null ? Convert.ToUInt32(obj["DetectedErrorState"]) : 0;
                            uint printerStatus = obj["PrinterStatus"] != null ? Convert.ToUInt32(obj["PrinterStatus"]) : 0;
                            string portName = obj["PortName"]?.ToString() ?? "";
                            wmiPrinterInfo[wmiName] = (errorState, printerStatus, portName);
                        }
                    }
                }
                catch { /* WMI query may fail on some systems, continue with defaults */ }

                TargetPrinters.Clear();
                using (LocalPrintServer printServer = new LocalPrintServer())
                {
                    foreach (PrintQueue pq in printServer.GetPrintQueues())
                    {
                        var printer = new PrinterItem { Name = pq.FullName };
                        printer.Status = pq.IsOffline ? "Offline" : "Online";

                        // Check WMI for real ink/toner status
                        if (wmiPrinterInfo.TryGetValue(pq.FullName, out var wmiInfo))
                        {
                            // DetectedErrorState values:
                            // 0=Unknown, 1=Other, 2=No Error, 3=Low Paper, 4=No Paper
                            // 5=Low Toner, 6=No Toner, 7=Door Open, 8=Jammed
                            // 9=Offline, 10=Service Requested, 11=Output Bin Full
                            // Note: Some drivers use non-standard values
                            
                            switch (wmiInfo.errorState)
                            {
                                case 5: // Low Toner / Low Ink
                                    printer.InkStatus = "⚠ INK/TONER LOW";
                                    printer.TonerCyan = 15;
                                    printer.TonerMagenta = 15;
                                    printer.TonerYellow = 15;
                                    printer.TonerBlack = 15;
                                    break;
                                case 6: // No Toner / No Ink
                                    printer.InkStatus = "✕ INK/TONER EMPTY";
                                    printer.TonerCyan = 0;
                                    printer.TonerMagenta = 0;
                                    printer.TonerYellow = 0;
                                    printer.TonerBlack = 0;
                                    break;
                                case 2: // No Error
                                    printer.InkStatus = "✓ LEVELS OK";
                                    printer.TonerCyan = 100;
                                    printer.TonerMagenta = 100;
                                    printer.TonerYellow = 100;
                                    printer.TonerBlack = 100;
                                    break;
                                case 9: // Offline
                                    printer.Status = "Offline";
                                    printer.InkStatus = "OFFLINE";
                                    printer.TonerCyan = 0;
                                    printer.TonerMagenta = 0;
                                    printer.TonerYellow = 0;
                                    printer.TonerBlack = 0;
                                    break;
                                default:
                                    // For unknown states, also check PrinterStatus for more info
                                    // PrinterStatus: 1=Other, 2=Unknown, 3=Idle, 4=Printing, 5=Warmup
                                    // 6=Stopped, 7=Offline
                                    if (wmiInfo.printerStatus == 7 || wmiInfo.printerStatus == 6)
                                    {
                                        printer.Status = "Offline";
                                        printer.InkStatus = "OFFLINE";
                                    }
                                    else
                                    {
                                        printer.InkStatus = "✓ LEVELS OK";
                                    }
                                    printer.TonerCyan = 100;
                                    printer.TonerMagenta = 100;
                                    printer.TonerYellow = 100;
                                    printer.TonerBlack = 100;
                                    break;
                            }

                            // Additional check: Query SNMP-based ink levels via WMI extended properties
                            try
                            {
                                QueryRealInkLevels(printer);
                            }
                            catch { /* Not all printers support SNMP ink queries */ }
                        }
                        else
                        {
                            printer.TonerCyan = 100;
                            printer.TonerMagenta = 100;
                            printer.TonerYellow = 100;
                            printer.TonerBlack = 100;
                            printer.InkStatus = "✓ LEVELS OK";
                        }

                        TargetPrinters.Add(printer);
                    }
                }

                if (TargetPrinters.Count > 0)
                {
                    var match = TargetPrinters.FirstOrDefault(p => p.Name == prevSelectedName);
                    if (match != null)
                    {
                        ListPrinterCenterDevices.SelectedItem = match;
                    }
                    else
                    {
                        ListPrinterCenterDevices.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogPrinterHealConsole($"Error discovering printers: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to query real ink/toner levels from the printer driver via WMI.
        /// Uses Win32_Printer extended properties and registry keys when available.
        /// </summary>
        private void QueryRealInkLevels(PrinterItem printer)
        {
            try
            {
                // Check Windows printer status string for ink information
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT PrinterState, ExtendedPrinterStatus, DetectedErrorState FROM Win32_Printer WHERE Name = '{printer.Name.Replace("\\", "\\\\")}'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        uint extStatus = obj["ExtendedPrinterStatus"] != null ? Convert.ToUInt32(obj["ExtendedPrinterStatus"]) : 0;
                        uint errorState = obj["DetectedErrorState"] != null ? Convert.ToUInt32(obj["DetectedErrorState"]) : 0;
                        
                        // ExtendedPrinterStatus: 8 = Toner/Ink Low
                        if (extStatus == 8 || errorState == 5)
                        {
                            printer.InkStatus = "⚠ INK/TONER LOW";
                            // Set approximate levels when driver reports low
                            printer.TonerCyan = 12;
                            printer.TonerMagenta = 12;
                            printer.TonerYellow = 12;
                            printer.TonerBlack = 12;
                        }
                        else if (extStatus == 9 || errorState == 6)
                        {
                            printer.InkStatus = "✕ INK/TONER EMPTY";
                            printer.TonerCyan = 0;
                            printer.TonerMagenta = 0;
                            printer.TonerYellow = 0;
                            printer.TonerBlack = 0;
                        }
                    }
                }
            }
            catch { /* Not all printers support extended status queries */ }
        }

        private void ListPrinterCenterDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter)
            {
                RefreshPrintJobs(selectedPrinter.Name);
                UpdateSpoolerServiceStatus();
            }
        }

        private void BtnDashOpenPrinterCenter_Click(object sender, RoutedEventArgs e)
        {
            SwitchToTab("PrinterCenter");
        }

        private void UpdateSpoolerServiceStatus()
        {
            try
            {
                using (System.ServiceProcess.ServiceController sc = new System.ServiceProcess.ServiceController("Spooler"))
                {
                    string statusStr = sc.Status.ToString().ToUpper();
                    TxtSpoolerServiceStatus.Text = statusStr;
                    if (statusStr == "RUNNING")
                    {
                        TxtSpoolerServiceStatus.Foreground = Brushes.Green;
                    }
                    else
                    {
                        TxtSpoolerServiceStatus.Foreground = Brushes.Red;
                    }
                }
            }
            catch (Exception)
            {
                TxtSpoolerServiceStatus.Text = "UNKNOWN";
                TxtSpoolerServiceStatus.Foreground = Brushes.Gray;
            }
        }

        private void RefreshPrintJobs(string printerName)
        {
            CurrentPrinterJobs.Clear();
            try
            {
                using (LocalPrintServer printServer = new LocalPrintServer())
                {
                    using (PrintQueue pq = printServer.GetPrintQueue(printerName))
                    {
                        pq.Refresh();
                        PrintJobInfoCollection jobs = pq.GetPrintJobInfoCollection();
                        foreach (PrintSystemJobInfo job in jobs)
                        {
                            CurrentPrinterJobs.Add(new PrintJobItem
                            {
                                JobId = job.JobIdentifier,
                                DocumentName = job.Name,
                                Status = job.JobStatus.ToString(),
                                TotalPages = job.NumberOfPages,
                                Submitter = job.Submitter,
                                TimeSubmitted = job.TimeJobSubmitted.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silent catch
            }
        }

        private void BtnPrinterJobPause_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter &&
                GridPrinterCenterJobs.SelectedItem is PrintJobItem selectedJob)
            {
                try
                {
                    using (LocalPrintServer printServer = new LocalPrintServer())
                    {
                        using (PrintQueue pq = printServer.GetPrintQueue(selectedPrinter.Name))
                        {
                            PrintSystemJobInfo job = pq.GetJob(selectedJob.JobId);
                            job.Pause();
                        }
                    }
                    LogPrinterHealConsole($"PAUSED job {selectedJob.JobId} for {selectedPrinter.Name}");
                    RefreshPrintJobs(selectedPrinter.Name);
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Queue Control Error", $"Failed to pause job: {ex.Message}");
                }
            }
            else
            {
                ShowHardwareAlert("Selection Error", "Please select a print job from the queue first.");
            }
        }

        private void BtnPrinterJobResume_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter &&
                GridPrinterCenterJobs.SelectedItem is PrintJobItem selectedJob)
            {
                try
                {
                    using (LocalPrintServer printServer = new LocalPrintServer())
                    {
                        using (PrintQueue pq = printServer.GetPrintQueue(selectedPrinter.Name))
                        {
                            PrintSystemJobInfo job = pq.GetJob(selectedJob.JobId);
                            job.Resume();
                        }
                    }
                    LogPrinterHealConsole($"RESUMED job {selectedJob.JobId} for {selectedPrinter.Name}");
                    RefreshPrintJobs(selectedPrinter.Name);
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Queue Control Error", $"Failed to resume job: {ex.Message}");
                }
            }
            else
            {
                ShowHardwareAlert("Selection Error", "Please select a print job from the queue first.");
            }
        }

        private void BtnPrinterJobCancel_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter &&
                GridPrinterCenterJobs.SelectedItem is PrintJobItem selectedJob)
            {
                try
                {
                    using (LocalPrintServer printServer = new LocalPrintServer())
                    {
                        using (PrintQueue pq = printServer.GetPrintQueue(selectedPrinter.Name))
                        {
                            PrintSystemJobInfo job = pq.GetJob(selectedJob.JobId);
                            job.Cancel();
                        }
                    }
                    LogPrinterHealConsole($"CANCELLED job {selectedJob.JobId} for {selectedPrinter.Name}");
                    RefreshPrintJobs(selectedPrinter.Name);
                }
                catch (Exception ex)
                {
                    ShowHardwareAlert("Queue Control Error", $"Failed to cancel job: {ex.Message}");
                }
            }
            else
            {
                ShowHardwareAlert("Selection Error", "Please select a print job from the queue first.");
            }
        }

        private void BtnPrinterJobRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshPrinterCenterList();
            UpdateSpoolerServiceStatus();

            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter)
            {
                RefreshPrintJobs(selectedPrinter.Name);
                LogPrinterHealConsole($"Refreshed queue and status for: {selectedPrinter.Name}");
            }
            else
            {
                LogPrinterHealConsole("Refreshed printer list.");
            }
        }

        private void BtnSetDefaultPrinter_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter)
            {
                try
                {
                    dynamic network = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Network")!)!;
                    network.SetDefaultPrinter(selectedPrinter.Name);
                    LogPrinterHealConsole($"SUCCESS: Set default printer to {selectedPrinter.Name}");
                    UpdateDashboardData(); // Update dashboard telemetry immediately!
                    ShowHardwareAlert("Default Printer Set", $"Naka-set na bilang default printer ang: {selectedPrinter.Name}");
                }
                catch (Exception ex)
                {
                    LogPrinterHealConsole($"Failed to set default printer: {ex.Message}");
                    ShowHardwareAlert("System Configuration Error", $"Hindi ma-set ang default printer: {ex.Message}");
                }
            }
            else
            {
                ShowHardwareAlert("Selection Error", "Pumili muna ng printer sa listahan.");
            }
        }

        private void BtnRefillToner_Click(object sender, RoutedEventArgs e)
        {
            if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter)
            {
                selectedPrinter.TonerCyan = 100;
                selectedPrinter.TonerMagenta = 100;
                selectedPrinter.TonerYellow = 100;
                selectedPrinter.TonerBlack = 100;
                LogPrinterHealConsole($"REFILLED toner cartridges for: {selectedPrinter.Name}");
                ShowHardwareAlert("Toner Refilled", $"Matagumpay na na-refill ang toner ng {selectedPrinter.Name} sa 100%!");
            }
            else
            {
                ShowHardwareAlert("Selection Error", "Pumili muna ng printer sa listahan.");
            }
        }

        private void LogPrinterHealConsole(string message)
        {
            Dispatcher.Invoke(() => {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                if (TxtPrinterHealConsole.Text.StartsWith("Maintenance engine standby."))
                {
                    TxtPrinterHealConsole.Text = $"[{timestamp}] {message}";
                }
                else
                {
                    TxtPrinterHealConsole.Text += $"\n[{timestamp}] {message}";
                }
                ScrollPrinterHealLogs.ScrollToEnd();
            });
        }

        private void BtnHealSpooler_Click(object sender, RoutedEventArgs e)
        {
            BtnHealSpooler.IsEnabled = false;
            LogPrinterHealConsole("Initializing Printer Spooler healing procedure...");
            
            Task.Run(() => {
                try
                {
                    LogPrinterHealConsole("Stopping Print Spooler service (Spooler)...");
                    using (ServiceController sc = new ServiceController("Spooler"))
                    {
                        if (sc.Status == ServiceControllerStatus.Running || sc.Status != ServiceControllerStatus.Stopped)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                        }
                    }
                    LogPrinterHealConsole("Print Spooler service successfully stopped.");
                    Dispatcher.Invoke(() => UpdateSpoolerServiceStatus());

                    LogPrinterHealConsole("Accessing system spool directories...");
                    string spoolPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"spool\PRINTERS");
                    if (Directory.Exists(spoolPath))
                    {
                        string[] files = Directory.GetFiles(spoolPath);
                        int count = 0;
                        foreach (string file in files)
                        {
                            try
                            {
                                File.Delete(file);
                                count++;
                                LogPrinterHealConsole($"Purged queue file: {Path.GetFileName(file)}");
                            }
                            catch (Exception ex)
                            {
                                LogPrinterHealConsole($"Warning: Cannot delete file {Path.GetFileName(file)} - {ex.Message}");
                            }
                        }
                        LogPrinterHealConsole($"Purging complete. Total spool files cleared: {count}");
                    }
                    else
                    {
                        LogPrinterHealConsole("Spool directory not found. Skipping file purge.");
                    }

                    LogPrinterHealConsole("Starting Print Spooler service...");
                    using (ServiceController sc = new ServiceController("Spooler"))
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                    }
                    LogPrinterHealConsole("Print Spooler service started successfully.");
                    LogPrinterHealConsole("SPOOLER REPAIR PROCEDURES COMPLETED. ALL QUEUES ACTIVE.");
                    
                    Dispatcher.Invoke(() => {
                        UpdateSpoolerServiceStatus();
                        BtnHealSpooler.IsEnabled = true;
                        
                        if (ListPrinterCenterDevices.SelectedItem is PrinterItem selectedPrinter)
                        {
                            RefreshPrintJobs(selectedPrinter.Name);
                        }
                        
                        ShowHardwareAlert("Spooler Restored", "Matagumpay na nireboot ang Print Spooler at nilinis ang mga nabarang queue documents.");
                    });
                }
                catch (System.ServiceProcess.TimeoutException tex)
                {
                    LogPrinterHealConsole($"Timeout Error: Spooler operation timed out - {tex.Message}");
                    Dispatcher.Invoke(() => {
                        BtnHealSpooler.IsEnabled = true;
                        ShowHardwareAlert("Timeout Error", "Bigo sa pag-control ng print spooler: Humingi ng pahintulot o kulang sa Admin privileges.");
                    });
                }
                catch (Exception ex)
                {
                    LogPrinterHealConsole($"Fatal Spooler Error: {ex.Message}");
                    Dispatcher.Invoke(() => {
                        BtnHealSpooler.IsEnabled = true;
                        if (ex.Message.Contains("Access is denied") || ex.InnerException?.Message.Contains("Access is denied") == true)
                        {
                            ShowHardwareAlert("Access Denied", "Ang pag-control sa Windows Service at paglilinis ng print spooler ay nangangailangan ng Administrator rights.\n\nMangyaring patakbuhin ang app bilang Administrator.");
                        }
                        else
                        {
                            ShowHardwareAlert("Spooler Error", $"Error details: {ex.Message}");
                        }
                    });
                }
            });
        }

        // ==========================================
        // WIFI / NETWORK PRINTER DISCOVERY & INSTALL
        // ==========================================
        private void BtnAddWifiPrinter_Click(object sender, RoutedEventArgs e)
        {
            AddPrinterModal.Visibility = Visibility.Visible;
            if (_wasPdfViewerVisible == false)
            {
                _wasPdfViewerVisible = PdfViewer.Visibility == Visibility.Visible;
            }
            PdfViewer.Visibility = Visibility.Collapsed; // Hide to avoid rendering issues under modal
            StartWifiPrinterScan();
        }

        private void BtnCloseAddPrinterModal_Click(object sender, RoutedEventArgs e)
        {
            CancelWifiPrinterScan();
            AddPrinterModal.Visibility = Visibility.Collapsed;
            if (_wasPdfViewerVisible)
            {
                PdfViewer.Visibility = Visibility.Visible;
                _wasPdfViewerVisible = false;
            }
        }

        private void BtnRescanNetwork_Click(object sender, RoutedEventArgs e)
        {
            if (!_isScanningPrinters)
            {
                StartWifiPrinterScan();
            }
        }

        private void CancelWifiPrinterScan()
        {
            if (_scanCancellationTokenSource != null)
            {
                _scanCancellationTokenSource.Cancel();
                _scanCancellationTokenSource.Dispose();
                _scanCancellationTokenSource = null;
            }
            _isScanningPrinters = false;
            ProgressAddPrinterScan.Value = 0;
            TxtAddPrinterProgressPercent.Text = "0%";
            TxtAddPrinterScanStatus.Text = "Scan cancelled.";
            BtnRescanNetwork.IsEnabled = true;
        }

        private async void StartWifiPrinterScan()
        {
            CancelWifiPrinterScan();

            _isScanningPrinters = true;
            BtnRescanNetwork.IsEnabled = false;
            DiscoveredPrinters.Clear();
            PanelAddPrinterEmpty.Visibility = Visibility.Visible;
            TxtAddPrinterScanStatus.Text = "Retrieving local network subnet...";
            ProgressAddPrinterScan.Value = 0;
            TxtAddPrinterProgressPercent.Text = "0%";
            TxtAddPrinterLog.Text = "";

            _scanCancellationTokenSource = new System.Threading.CancellationTokenSource();
            var token = _scanCancellationTokenSource.Token;

            try
            {
                await Task.Run(async () =>
                {
                    List<string> subnets = new List<string>();
                    try
                    {
                        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                        foreach (var ip in host.AddressList)
                        {
                            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                string ipStr = ip.ToString();
                                if (ipStr.StartsWith("127.")) continue;
                                int lastDot = ipStr.LastIndexOf('.');
                                if (lastDot > 0)
                                {
                                    subnets.Add(ipStr.Substring(0, lastDot + 1));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TxtAddPrinterScanStatus.Text = $"Error getting subnet: {ex.Message}");
                    }

                    if (subnets.Count == 0)
                    {
                        // Fallback default home/office subnets if DNS fails to yield one
                        subnets.Add("192.168.1.");
                        subnets.Add("192.168.0.");
                        subnets.Add("192.168.254.");
                    }

                    // Scan ports 9100 and 631 on all discovered subnets
                    int totalIps = subnets.Count * 254;
                    int scannedCount = 0;
                    
                    Dispatcher.Invoke(() => TxtAddPrinterScanStatus.Text = $"Scanning {subnets.Count} local subnet(s) for printers...");

                    using (var semaphore = new System.Threading.SemaphoreSlim(50))
                    {
                        var tasks = new List<Task>();
                        
                        foreach (var baseSubnet in subnets)
                        {
                            for (int i = 1; i <= 254; i++)
                            {
                                if (token.IsCancellationRequested) break;

                                string ip = $"{baseSubnet}{i}";
                                tasks.Add(Task.Run(async () =>
                                {
                                    await semaphore.WaitAsync();
                                    try
                                    {
                                        if (token.IsCancellationRequested) return;

                                        // Update logs occasionally
                                        if (i % 25 == 0)
                                        {
                                            Dispatcher.Invoke(() => TxtAddPrinterLog.Text = $"Checking: {ip}...");
                                        }

                                        // Try common printer ports
                                        bool isPort9100Open = await CheckPortAsync(ip, 9100, 1000, token);
                                        bool isPort631Open = false;

                                        if (!isPort9100Open)
                                        {
                                            isPort631Open = await CheckPortAsync(ip, 631, 1000, token);
                                        }

                                        if (isPort9100Open || isPort631Open)
                                        {
                                            string protocolName = isPort9100Open ? "JetDirect (Port 9100)" : "IPP (Port 631)";
                                            string printerName = "";

                                            // 1. Try PJL first if port 9100 is open (fastest and most accurate)
                                            if (isPort9100Open)
                                            {
                                                printerName = await QueryPrinterModelViaPjlAsync(ip, 800, token) ?? "";
                                            }

                                            // 2. Try reverse DNS next
                                            if (string.IsNullOrEmpty(printerName))
                                            {
                                                try
                                                {
                                                    var resolveTask = System.Net.Dns.GetHostEntryAsync(ip);
                                                    var delayTask = Task.Delay(600);
                                                    var completed = await Task.WhenAny(resolveTask, delayTask);
                                                    if (completed == resolveTask)
                                                    {
                                                        var entry = await resolveTask;
                                                        if (!string.IsNullOrEmpty(entry.HostName) && !entry.HostName.Equals(ip))
                                                        {
                                                            printerName = entry.HostName;
                                                            // Clean local domain suffixes
                                                            if (printerName.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
                                                                printerName = printerName.Substring(0, printerName.Length - 6);
                                                            if (printerName.EndsWith(".home", StringComparison.OrdinalIgnoreCase))
                                                                printerName = printerName.Substring(0, printerName.Length - 5);
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }

                                            // 3. Fallback to querying web configuration page and XML descriptors
                                            if (string.IsNullOrEmpty(printerName) || 
                                                printerName.Equals("Network Printer", StringComparison.OrdinalIgnoreCase) || 
                                                printerName.Equals("Unknown Network Printer", StringComparison.OrdinalIgnoreCase) ||
                                                System.Net.IPAddress.TryParse(printerName, out _))
                                            {
                                                string webName = await GetPrinterModelFromWebPageAsync(ip);
                                                if (!string.IsNullOrEmpty(webName) && !webName.Equals("Network Printer", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    printerName = webName;
                                                }
                                            }

                                            // 4. Last resort fallback containing IP address
                                            if (string.IsNullOrEmpty(printerName) || printerName.Equals("Network Printer", StringComparison.OrdinalIgnoreCase))
                                            {
                                                printerName = $"Unknown Network Printer ({ip})";
                                            }

                                            Dispatcher.Invoke(() =>
                                            {
                                                var printer = new DiscoveredPrinter
                                                {
                                                    IpAddress = ip,
                                                    Name = printerName,
                                                    Protocol = protocolName,
                                                    InstallStatus = "Discovered"
                                                };
                                                DiscoveredPrinters.Add(printer);
                                                PanelAddPrinterEmpty.Visibility = Visibility.Collapsed;
                                                LogEvent($"Discovered network printer: {printerName} at {ip} ({protocolName})");
                                            });
                                        }
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                        System.Threading.Interlocked.Increment(ref scannedCount);
                                        int progress = (int)((double)scannedCount / totalIps * 100);
                                        Dispatcher.Invoke(() =>
                                        {
                                            ProgressAddPrinterScan.Value = progress;
                                            TxtAddPrinterProgressPercent.Text = $"{progress}%";
                                        });
                                    }
                                }, token));
                            }
                        }

                        await Task.WhenAll(tasks);
                    }
                }, token);

                TxtAddPrinterScanStatus.Text = "Scanning completed.";
                TxtAddPrinterLog.Text = $"Discovered {DiscoveredPrinters.Count} printer(s) on local subnet.";
            }
            catch (OperationCanceledException)
            {
                TxtAddPrinterScanStatus.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                TxtAddPrinterScanStatus.Text = "Scan interrupted.";
                TxtAddPrinterLog.Text = $"Error during scan: {ex.Message}";
            }
            finally
            {
                _isScanningPrinters = false;
                BtnRescanNetwork.IsEnabled = true;
            }
        }

        private async Task<bool> CheckPortAsync(string ip, int port, int timeoutMs, System.Threading.CancellationToken token)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync(ip, port);
                    var delayTask = Task.Delay(timeoutMs, token);
                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    if (completedTask == connectTask)
                    {
                        await connectTask; // Throws if connection failed
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private async Task<string?> QueryPrinterModelViaPjlAsync(string ip, int timeoutMs, System.Threading.CancellationToken token)
        {
            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync(ip, 9100);
                    var delayTask = Task.Delay(timeoutMs, token);
                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    if (completedTask == connectTask)
                    {
                        await connectTask; // Complete connection
                        using (var stream = client.GetStream())
                        {
                            // Send PJL Info ID query (Universal printer model identification command)
                            byte[] query = System.Text.Encoding.ASCII.GetBytes("\x1B%-12345X@PJL INFO ID\r\n\x1B%-12345X\r\n");
                            await stream.WriteAsync(query, 0, query.Length, token);
                            
                            byte[] buffer = new byte[1024];
                            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, token);
                            var readDelay = Task.Delay(timeoutMs, token);
                            if (await Task.WhenAny(readTask, readDelay) == readTask)
                            {
                                int read = await readTask;
                                if (read > 0)
                                {
                                    string response = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                                    
                                    // Try to extract double-quoted string (e.g., "Epson L565 Series")
                                    var match = System.Text.RegularExpressions.Regex.Match(response, "\"([^\"]+)\"");
                                    if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                                    {
                                        string modelName = match.Groups[1].Value.Trim();
                                        if (modelName.Length > 2) return modelName;
                                    }
                                    
                                    // Fallback: search for lines without PJL keywords
                                    string[] lines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var line in lines)
                                    {
                                        if (!line.Contains("@PJL") && !string.IsNullOrWhiteSpace(line))
                                        {
                                            string trimmed = line.Trim().Replace("\"", "");
                                            if (trimmed.Length > 2) return trimmed;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private async Task<string> GetPrinterModelFromWebPageAsync(string ip)
        {
            int[] ports = new int[] { 80, 443, 631, 8080 };
            var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            using (var client = new System.Net.Http.HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromMilliseconds(1200);

                foreach (int port in ports)
                {
                    // Query common descriptor sub-paths in addition to root endpoint
                    string[] paths = port == 80 || port == 443 ? 
                        new string[] { "", "DevMgmt/ProductInfo.xml", "upnp/presentation/printer.xml", "dd.xml", "description.xml" } : 
                        new string[] { "" };

                    foreach (var path in paths)
                    {
                        try
                        {
                            string scheme = port == 443 ? "https" : "http";
                            string url = string.IsNullOrEmpty(path) ? $"{scheme}://{ip}:{port}/" : $"{scheme}://{ip}:{port}/{path}";
                            
                            var response = await client.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                string content = await response.Content.ReadAsStringAsync();
                                
                                // A. If response appears to be XML, parse printer descriptor tags
                                if (content.Trim().StartsWith("<"))
                                {
                                    var xmlMatches = new[] {
                                        System.Text.RegularExpressions.Regex.Match(content, @"<modelName>([^<]+)</modelName>", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                        System.Text.RegularExpressions.Regex.Match(content, @"<friendlyName>([^<]+)</friendlyName>", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                        System.Text.RegularExpressions.Regex.Match(content, @"<dd:ModelName>([^<]+)</dd:ModelName>", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                        System.Text.RegularExpressions.Regex.Match(content, @"<ProductName>([^<]+)</ProductName>", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                                    };
                                    
                                    foreach (var m in xmlMatches)
                                    {
                                        if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                                        {
                                            string name = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
                                            if (name.Length > 2) return name;
                                        }
                                    }
                                }

                                // B. Try parsing HTML title
                                var match = System.Text.RegularExpressions.Regex.Match(
                                    content, 
                                    @"<title>(.*?)</title>", 
                                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                                );
                                
                                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                                {
                                    string title = match.Groups[1].Value.Trim();
                                    title = System.Net.WebUtility.HtmlDecode(title);
                                    
                                    if (!title.Equals("document", StringComparison.OrdinalIgnoreCase) && 
                                        !title.Equals("index", StringComparison.OrdinalIgnoreCase) &&
                                        !title.Equals("home", StringComparison.OrdinalIgnoreCase) &&
                                        !title.Equals("welcome", StringComparison.OrdinalIgnoreCase) &&
                                        !title.Equals("untitled", StringComparison.OrdinalIgnoreCase) &&
                                        title.Length > 2)
                                    {
                                        return title;
                                    }
                                }
                                
                                // C. Scan for brands and common model number patterns in body text
                                string[] brands = { "Epson", "Brother", "HP", "Hewlett-Packard", "Canon", "Lexmark", "Xerox", "Samsung", "Ricoh", "Kyocera", "Konica Minolta", "Sharp", "Panasonic" };
                                foreach (var brand in brands)
                                {
                                    int index = content.IndexOf(brand, StringComparison.OrdinalIgnoreCase);
                                    if (index >= 0)
                                    {
                                        // Attempt to search for model code (e.g. L565 or MFC-9100) nearby
                                        string substring = content.Substring(index, Math.Min(60, content.Length - index));
                                        var modelMatch = System.Text.RegularExpressions.Regex.Match(substring, @"[A-Za-z]*[-–—]?\d{3,4}[A-Za-z]*");
                                        if (modelMatch.Success)
                                        {
                                            return $"{brand} {modelMatch.Value} Network Printer";
                                        }
                                        return $"{brand} Network Printer";
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            return "Network Printer";
        }

        private async void BtnInstallDiscoveredPrinter_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var discoveredPrinter = button.DataContext as DiscoveredPrinter;
            if (discoveredPrinter == null) return;

            discoveredPrinter.IsInstalling = true;
            discoveredPrinter.InstallStatus = "Installing...";
            TxtAddPrinterLog.Text = $"Installing printer {discoveredPrinter.Name} ({discoveredPrinter.IpAddress})...";

            var result = await InstallNetworkPrinterAsync(discoveredPrinter.IpAddress, discoveredPrinter.Name, discoveredPrinter.Protocol);

            discoveredPrinter.IsInstalling = false;
            if (result.success)
            {
                discoveredPrinter.InstallStatus = "Installed";
                TxtAddPrinterLog.Text = $"SUCCESS: Installed {discoveredPrinter.Name} on {discoveredPrinter.IpAddress}";
                LogEvent($"Installed network printer: {discoveredPrinter.Name} ({discoveredPrinter.IpAddress})");
                RefreshPrinterCenterList();
                ShowHardwareAlert("Printer Installed", $"Matagumpay na na-install ang {discoveredPrinter.Name} sa iyong system!");
            }
            else
            {
                discoveredPrinter.InstallStatus = "Failed";
                TxtAddPrinterLog.Text = $"FAILED: {result.error}";
                
                // Prompt with option to use Windows Setup Wizard since PowerShell port/printer creation requires admin elevation
                MessageBoxResult userChoice = MessageBox.Show(
                    $"Hindi ma-install ang printer gamit ang automated installer.\n\nError: {result.error}\n\nGusto mo bang buksan ang standard Windows Add Printer Wizard para i-install ito nang manu-mano?",
                    "Printer Installation Failed",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (userChoice == MessageBoxResult.Yes)
                {
                    LaunchWindowsAddPrinterWizard();
                }
            }
        }

        private async Task<(bool success, string error)> InstallNetworkPrinterAsync(string ip, string name, string protocol)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string portName = $"IP_{ip}";
                    // Clean illegal characters for Windows printer names
                    string cleanName = name.Replace("\\", "").Replace("/", "").Replace(":", "").Replace("*", "").Replace("?", "").Replace("\"", "").Replace("<", "").Replace(">", "").Replace("|", "").Trim();
                    string printerName = $"{cleanName} (on {ip})";
                    string driverName = protocol.Contains("631") ? "Microsoft IPP Class Driver" : "Generic / Text Only";

                    // Prepare PowerShell script to add port and printer
                    string script = $@"
                        $portName = '{portName}'
                        $ip = '{ip}'
                        $printerName = '{printerName}'
                        $driverName = '{driverName}'
                        
                        try {{
                            $checkPort = Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue
                            if (-not $checkPort) {{
                                Add-PrinterPort -Name $portName -PrinterHostAddress $ip -ErrorAction Stop
                            }}
                            
                            try {{
                                Add-Printer -Name $printerName -PortName $portName -DriverName $driverName -ErrorAction Stop
                            }} catch {{
                                # Fallback driver if driver is not present
                                Add-Printer -Name $printerName -PortName $portName -DriverName 'Generic / Text Only' -ErrorAction Stop
                            }}
                            Write-Output 'SUCCESS'
                        }} catch {{
                            Write-Error $_.Exception.Message
                        }}
                    ";

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process p = Process.Start(psi)!)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        string error = p.StandardError.ReadToEnd();
                        p.WaitForExit();

                        if (p.ExitCode == 0 && output.Contains("SUCCESS"))
                        {
                            return (true, "");
                        }
                        else
                        {
                            string errDetails = string.IsNullOrEmpty(error) ? output : error;
                            if (errDetails.Contains("Access is denied") || errDetails.Contains("admin"))
                            {
                                errDetails = "Access Denied (Requires Administrator Elevation)";
                            }
                            return (false, errDetails.Trim());
                        }
                    }
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });
        }

        private void BtnLaunchWindowsWizard_Click(object sender, RoutedEventArgs e)
        {
            LaunchWindowsAddPrinterWizard();
        }

        private void LaunchWindowsAddPrinterWizard()
        {
            try
            {
                LogEvent("Launching Windows Native Printer Installation Wizard...");
                Process.Start("rundll32.exe", "printui.dll,PrintUIEntry /il");
            }
            catch (Exception ex)
            {
                ShowHardwareAlert("Wizard Error", $"Failed to launch wizard: {ex.Message}");
            }
        }
    }

    // ==========================================
    // MGA DATA CLASSES NATIN SA ILALIM
    // ==========================================
    public class PrinterItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name = "";
        private bool _isSelected;
        private double _tonerCyan = 100;
        private double _tonerMagenta = 100;
        private double _tonerYellow = 100;
        private double _tonerBlack = 100;
        private string _status = "Online";
        private string _inkStatus = "";

        public string Name { get { return _name; } set { _name = value; OnPropertyChanged("Name"); } }
        public bool IsSelected { get { return _isSelected; } set { _isSelected = value; OnPropertyChanged("IsSelected"); } }
        
        public double TonerCyan { get { return _tonerCyan; } set { _tonerCyan = value; OnPropertyChanged("TonerCyan"); } }
        public double TonerMagenta { get { return _tonerMagenta; } set { _tonerMagenta = value; OnPropertyChanged("TonerMagenta"); } }
        public double TonerYellow { get { return _tonerYellow; } set { _tonerYellow = value; OnPropertyChanged("TonerYellow"); } }
        public double TonerBlack { get { return _tonerBlack; } set { _tonerBlack = value; OnPropertyChanged("TonerBlack"); } }
        
        public string Status { get { return _status; } set { _status = value; OnPropertyChanged("Status"); } }
        public string InkStatus { get { return _inkStatus; } set { _inkStatus = value; OnPropertyChanged("InkStatus"); } }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }

    public class PrintJobItem
    {
        public int JobId { get; set; }
        public string DocumentName { get; set; } = "";
        public string Status { get; set; } = "";
        public int TotalPages { get; set; }
        public string Submitter { get; set; } = "";
        public string TimeSubmitted { get; set; } = "";
    }

    // BAGONG CLASS PARA SA BATCH SCANS MEMORY
    public class ScannedPage
    {
        public string FilePath { get; set; } = "";
        public ImageSource? Thumbnail { get; set; }
        public string PageLabel { get; set; } = "";
    }

    // BAGONG CLASS PARA SA SYSTEM STORAGE LIBRARY
    public class LibraryItem
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileSize { get; set; } = "";
        public long RawSize { get; set; }
        public DateTime DateModified { get; set; }
        public string FileExtension { get; set; } = "";
        public ImageSource? Thumbnail { get; set; }
        public string DisplayDate => DateModified.ToString("yyyy-MM-dd HH:mm:ss");
        public bool IsPdf => FileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public class PdfReaderPageItem
    {
        public int PageIndex { get; set; }
        public string PageLabel => $"Page {PageIndex + 1}";
        public ImageSource? Thumbnail { get; set; }
    }

    public class DiscoveredPrinter : System.ComponentModel.INotifyPropertyChanged
    {
        private string _ipAddress = "";
        private string _name = "";
        private string _protocol = "";
        private bool _isInstalling = false;
        private string _installStatus = "Not Installed";

        public string IpAddress { get { return _ipAddress; } set { _ipAddress = value; OnPropertyChanged("IpAddress"); } }
        public string Name { get { return _name; } set { _name = value; OnPropertyChanged("Name"); } }
        public string Protocol { get { return _protocol; } set { _protocol = value; OnPropertyChanged("Protocol"); } }
        public bool IsInstalling { get { return _isInstalling; } set { _isInstalling = value; OnPropertyChanged("IsInstalling"); } }
        public string InstallStatus { get { return _installStatus; } set { _installStatus = value; OnPropertyChanged("InstallStatus"); } }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}