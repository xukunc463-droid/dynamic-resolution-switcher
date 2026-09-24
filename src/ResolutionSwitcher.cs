using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("分辨率切换器")]
[assembly: AssemblyProduct("分辨率切换器")]
[assembly: AssemblyDescription("动态读取并快速切换主显示器支持的分辨率")]
[assembly: AssemblyVersion("2.0.0.0")]

namespace ResolutionSwitcher
{
    internal struct DisplayMode
    {
        public int Width;
        public int Height;
        public int RefreshRate;

        public override string ToString()
        {
            return string.Format("{0} × {1}  @  {2} Hz", Width, Height, RefreshRate);
        }
    }

    internal sealed class SwitchResult
    {
        public bool Success;
        public bool AlreadyActive;
        public string Message;
    }

    internal static class DisplayManager
    {
        private const int EnumCurrentSettings = -1;
        private const uint CdsUpdateRegistry = 0x00000001;
        private const uint CdsTest = 0x00000002;
        private const uint DmPelsWidth = 0x00080000;
        private const uint DmPelsHeight = 0x00100000;
        private const uint DmDisplayFrequency = 0x00400000;
        private const uint DmInterlaced = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettings(
            string deviceName,
            int modeNumber,
            ref DevMode deviceMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettings(ref DevMode deviceMode, uint flags);

        private static DevMode CreateDevMode()
        {
            DevMode mode = new DevMode();
            mode.dmSize = (ushort)Marshal.SizeOf(typeof(DevMode));
            return mode;
        }

        public static DisplayMode GetCurrentMode()
        {
            DevMode mode = CreateDevMode();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
            {
                throw new InvalidOperationException("无法读取当前显示模式。");
            }

            DisplayMode result = new DisplayMode();
            result.Width = (int)mode.dmPelsWidth;
            result.Height = (int)mode.dmPelsHeight;
            result.RefreshRate = (int)mode.dmDisplayFrequency;
            return result;
        }

        public static List<DisplayMode> GetAvailableModes()
        {
            List<DisplayMode> modes = new List<DisplayMode>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int modeNumber = 0; ; modeNumber++)
            {
                DevMode candidate = CreateDevMode();
                if (!EnumDisplaySettings(null, modeNumber, ref candidate))
                {
                    break;
                }

                if ((candidate.dmDisplayFlags & DmInterlaced) != 0 ||
                    candidate.dmBitsPerPel < 32 ||
                    candidate.dmPelsWidth == 0 ||
                    candidate.dmPelsHeight == 0 ||
                    candidate.dmDisplayFrequency == 0)
                {
                    continue;
                }

                string key = string.Format(
                    "{0}x{1}@{2}",
                    candidate.dmPelsWidth,
                    candidate.dmPelsHeight,
                    candidate.dmDisplayFrequency);
                if (!seen.Add(key))
                {
                    continue;
                }

                DisplayMode mode = new DisplayMode();
                mode.Width = (int)candidate.dmPelsWidth;
                mode.Height = (int)candidate.dmPelsHeight;
                mode.RefreshRate = (int)candidate.dmDisplayFrequency;
                modes.Add(mode);
            }

            modes.Sort(delegate(DisplayMode left, DisplayMode right)
            {
                int widthOrder = right.Width.CompareTo(left.Width);
                if (widthOrder != 0)
                {
                    return widthOrder;
                }

                int heightOrder = right.Height.CompareTo(left.Height);
                if (heightOrder != 0)
                {
                    return heightOrder;
                }

                return right.RefreshRate.CompareTo(left.RefreshRate);
            });

            return modes;
        }

        private static bool TryFindMode(int width, int height, int refreshRate, out DevMode selectedMode)
        {
            for (int modeNumber = 0; ; modeNumber++)
            {
                DevMode candidate = CreateDevMode();
                if (!EnumDisplaySettings(null, modeNumber, ref candidate))
                {
                    break;
                }

                bool dimensionsMatch = candidate.dmPelsWidth == (uint)width &&
                                       candidate.dmPelsHeight == (uint)height;
                bool frequencyMatches = candidate.dmDisplayFrequency == (uint)refreshRate;
                bool colorDepthSupported = candidate.dmBitsPerPel >= 32;
                if (!dimensionsMatch || !frequencyMatches || !colorDepthSupported)
                {
                    continue;
                }

                if ((candidate.dmDisplayFlags & DmInterlaced) == 0)
                {
                    selectedMode = candidate;
                    return true;
                }
            }

            selectedMode = CreateDevMode();
            return false;
        }

        public static SwitchResult SwitchTo(int width, int height, int refreshRate)
        {
            DisplayMode current;
            try
            {
                current = GetCurrentMode();
            }
            catch (Exception ex)
            {
                return Failure(ex.Message);
            }

            if (current.Width == width && current.Height == height && current.RefreshRate == refreshRate)
            {
                return new SwitchResult
                {
                    Success = true,
                    AlreadyActive = true,
                    Message = "已经是目标分辨率。"
                };
            }

            DevMode targetMode;
            if (!TryFindMode(width, height, refreshRate, out targetMode))
            {
                return Failure(string.Format(
                    "显卡驱动未提供 {0}×{1} @ {2}Hz 模式。",
                    width,
                    height,
                    refreshRate));
            }

            targetMode.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

            int testResult = ChangeDisplaySettings(ref targetMode, CdsTest);
            if (testResult != 0)
            {
                return Failure(DescribeResult(testResult, "驱动测试失败"));
            }

            int applyResult = ChangeDisplaySettings(ref targetMode, CdsUpdateRegistry);
            if (applyResult != 0)
            {
                return Failure(DescribeResult(applyResult, "切换失败"));
            }

            Thread.Sleep(500);

            try
            {
                DisplayMode verified = GetCurrentMode();
                if (verified.Width != width || verified.Height != height || verified.RefreshRate != refreshRate)
                {
                    return Failure(string.Format(
                        "驱动返回成功，但当前模式是 {0}。",
                        verified));
                }
            }
            catch (Exception ex)
            {
                return Failure("切换后验证失败：" + ex.Message);
            }

            return new SwitchResult
            {
                Success = true,
                AlreadyActive = false,
                Message = string.Format("已切换到 {0}×{1} @ {2}Hz。", width, height, refreshRate)
            };
        }

        private static SwitchResult Failure(string message)
        {
            return new SwitchResult
            {
                Success = false,
                AlreadyActive = false,
                Message = message
            };
        }

        private static string DescribeResult(int result, string prefix)
        {
            string detail;
            switch (result)
            {
                case 1:
                    detail = "需要重启 Windows 才能生效";
                    break;
                case -1:
                    detail = "显卡驱动拒绝了该设置";
                    break;
                case -2:
                    detail = "不支持该显示模式";
                    break;
                case -3:
                    detail = "无法写入显示设置";
                    break;
                case -4:
                    detail = "参数标志无效";
                    break;
                case -5:
                    detail = "参数无效";
                    break;
                case -6:
                    detail = "双屏配置不支持此操作";
                    break;
                default:
                    detail = "未知错误码 " + result;
                    break;
            }

            return prefix + "：" + detail + "。";
        }
    }

    internal sealed class AnnouncingLabel : Label
    {
        public void SetAccessibleText(string value, bool announce)
        {
            bool changed = !string.Equals(Text, value, StringComparison.Ordinal);
            Text = value;
            AccessibleName = value;

            if (announce && changed && IsHandleCreated)
            {
                AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly AnnouncingLabel currentModeLabel;
        private readonly AnnouncingLabel statusLabel;
        private readonly ListView modeList;
        private readonly CheckBox currentRefreshOnlyCheckBox;
        private readonly Button applyButton;
        private readonly Button refreshButton;
        private List<DisplayMode> availableModes;
        private DisplayMode currentMode;
        private bool initialized;

        public MainForm()
        {
            Text = "动态分辨率切换器";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(680, 580);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            KeyPreview = true;
            BackColor = Color.FromArgb(244, 247, 250);
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = SystemIcons.Application;
            availableModes = new List<DisplayMode>();

            Label heading = new Label();
            heading.Text = "主显示器支持的分辨率";
            heading.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
            heading.ForeColor = Color.FromArgb(28, 38, 50);
            heading.AutoSize = true;
            heading.Location = new Point(24, 20);

            currentModeLabel = new AnnouncingLabel();
            currentModeLabel.SetAccessibleText("当前分辨率：正在读取…", false);
            currentModeLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            currentModeLabel.ForeColor = Color.FromArgb(42, 75, 105);
            currentModeLabel.BackColor = Color.White;
            currentModeLabel.BorderStyle = BorderStyle.FixedSingle;
            currentModeLabel.TextAlign = ContentAlignment.MiddleCenter;
            currentModeLabel.Location = new Point(24, 62);
            currentModeLabel.Size = new Size(632, 48);

            currentRefreshOnlyCheckBox = new CheckBox();
            currentRefreshOnlyCheckBox.Text = "只显示当前刷新率";
            currentRefreshOnlyCheckBox.Checked = true;
            currentRefreshOnlyCheckBox.AutoSize = true;
            currentRefreshOnlyCheckBox.Location = new Point(24, 122);
            currentRefreshOnlyCheckBox.AccessibleName = "只显示与当前模式相同刷新率的分辨率";
            currentRefreshOnlyCheckBox.CheckedChanged += delegate
            {
                if (initialized)
                {
                    PopulateModeList();
                    ShowModeCount();
                }
            };

            modeList = new ListView();
            modeList.View = View.Details;
            modeList.FullRowSelect = true;
            modeList.GridLines = true;
            modeList.HideSelection = false;
            modeList.MultiSelect = false;
            modeList.Location = new Point(24, 152);
            modeList.Size = new Size(632, 298);
            modeList.AccessibleName = "显卡支持的显示模式列表";
            modeList.Columns.Add("分辨率", 290, HorizontalAlignment.Left);
            modeList.Columns.Add("刷新率", 140, HorizontalAlignment.Left);
            modeList.Columns.Add("状态", 170, HorizontalAlignment.Left);
            modeList.SelectedIndexChanged += delegate { UpdateApplyButton(); };
            modeList.DoubleClick += delegate { ApplySelectedMode(); };
            modeList.KeyDown += OnModeListKeyDown;

            applyButton = new Button();
            applyButton.Text = "选择一个模式";
            applyButton.Location = new Point(24, 464);
            applyButton.Size = new Size(424, 46);
            applyButton.FlatStyle = FlatStyle.Flat;
            applyButton.FlatAppearance.BorderSize = 0;
            applyButton.BackColor = Color.FromArgb(34, 112, 184);
            applyButton.ForeColor = Color.White;
            applyButton.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
            applyButton.Cursor = Cursors.Hand;
            applyButton.Enabled = false;
            applyButton.AccessibleName = "切换到列表中选中的显示模式";
            applyButton.Click += delegate { ApplySelectedMode(); };

            refreshButton = new Button();
            refreshButton.Text = "重新读取模式  [F5]";
            refreshButton.Location = new Point(460, 464);
            refreshButton.Size = new Size(196, 46);
            refreshButton.FlatStyle = FlatStyle.Flat;
            refreshButton.BackColor = Color.White;
            refreshButton.ForeColor = Color.FromArgb(32, 42, 54);
            refreshButton.FlatAppearance.BorderColor = Color.FromArgb(177, 190, 203);
            refreshButton.Cursor = Cursors.Hand;
            refreshButton.AccessibleName = "重新读取显卡支持的显示模式";
            refreshButton.Click += delegate { ReloadModes(true); };

            Label helpLabel = new Label();
            helpLabel.Text = "双击或 Enter 切换 · F5 重新读取 · Esc 关闭";
            helpLabel.AutoSize = false;
            helpLabel.TextAlign = ContentAlignment.MiddleCenter;
            helpLabel.ForeColor = Color.FromArgb(88, 99, 111);
            helpLabel.Location = new Point(24, 516);
            helpLabel.Size = new Size(632, 24);

            statusLabel = new AnnouncingLabel();
            statusLabel.SetAccessibleText("正在等待读取显卡模式…", false);
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.ForeColor = Color.FromArgb(88, 99, 111);
            statusLabel.Location = new Point(24, 544);
            statusLabel.Size = new Size(632, 24);

            Controls.Add(heading);
            Controls.Add(currentModeLabel);
            Controls.Add(currentRefreshOnlyCheckBox);
            Controls.Add(modeList);
            Controls.Add(applyButton);
            Controls.Add(refreshButton);
            Controls.Add(helpLabel);
            Controls.Add(statusLabel);

            Shown += delegate { ReloadModes(true); };
            Activated += delegate
            {
                if (initialized)
                {
                    RefreshCurrentMode();
                }
            };
            KeyDown += OnFormKeyDown;
        }

        private static bool IsSameMode(DisplayMode left, DisplayMode right)
        {
            return left.Width == right.Width &&
                   left.Height == right.Height &&
                   left.RefreshRate == right.RefreshRate;
        }

        private static string ModeKey(DisplayMode mode)
        {
            return string.Format("{0}x{1}@{2}", mode.Width, mode.Height, mode.RefreshRate);
        }

        private void ReloadModes(bool announce)
        {
            SetControlsEnabled(false);
            Cursor = Cursors.WaitCursor;
            statusLabel.ForeColor = Color.FromArgb(88, 99, 111);
            statusLabel.SetAccessibleText("正在读取显卡支持的显示模式…", announce);
            statusLabel.Refresh();

            try
            {
                currentMode = DisplayManager.GetCurrentMode();
                availableModes = DisplayManager.GetAvailableModes();
                if (availableModes.Count == 0)
                {
                    throw new InvalidOperationException("显卡驱动没有返回可用的逐行扫描模式。");
                }

                initialized = true;
                UpdateCurrentModeLabel(announce);
                PopulateModeList();
                ShowModeCount();
            }
            catch (Exception ex)
            {
                initialized = false;
                currentModeLabel.SetAccessibleText("当前分辨率：读取失败", announce);
                ShowFailure(ex.Message);
            }
            finally
            {
                Cursor = Cursors.Default;
                SetControlsEnabled(true);
            }
        }

        private void PopulateModeList()
        {
            string previousSelection = null;
            if (modeList.SelectedItems.Count > 0)
            {
                previousSelection = ModeKey((DisplayMode)modeList.SelectedItems[0].Tag);
            }

            ListViewItem preferredItem = null;
            ListViewItem currentItem = null;
            modeList.BeginUpdate();
            modeList.Items.Clear();

            foreach (DisplayMode mode in availableModes)
            {
                if (currentRefreshOnlyCheckBox.Checked && mode.RefreshRate != currentMode.RefreshRate)
                {
                    continue;
                }

                bool isCurrent = IsSameMode(mode, currentMode);
                ListViewItem item = new ListViewItem(string.Format("{0} × {1}", mode.Width, mode.Height));
                item.SubItems.Add(mode.RefreshRate + " Hz");
                item.SubItems.Add(isCurrent ? "当前模式" : string.Empty);
                item.Tag = mode;
                item.ToolTipText = mode.ToString();

                if (isCurrent)
                {
                    item.BackColor = Color.FromArgb(224, 238, 250);
                    item.ForeColor = Color.FromArgb(20, 62, 98);
                    currentItem = item;
                }

                if (previousSelection != null && ModeKey(mode) == previousSelection)
                {
                    preferredItem = item;
                }

                modeList.Items.Add(item);
            }

            modeList.EndUpdate();

            ListViewItem itemToSelect = preferredItem ?? currentItem;
            if (itemToSelect == null && modeList.Items.Count > 0)
            {
                itemToSelect = modeList.Items[0];
            }

            if (itemToSelect != null)
            {
                itemToSelect.Selected = true;
                itemToSelect.Focused = true;
                itemToSelect.EnsureVisible();
            }

            UpdateApplyButton();
        }

        private void ShowModeCount()
        {
            statusLabel.ForeColor = Color.FromArgb(88, 99, 111);
            statusLabel.SetAccessibleText(string.Format(
                "当前显示 {0} 个模式；显卡共返回 {1} 个模式。",
                modeList.Items.Count,
                availableModes.Count), true);
        }

        private void UpdateCurrentModeLabel(bool announce)
        {
            currentModeLabel.SetAccessibleText("当前分辨率：" + currentMode, announce);
            currentRefreshOnlyCheckBox.Text = string.Format(
                "只显示当前刷新率（{0} Hz）",
                currentMode.RefreshRate);
        }

        private void UpdateApplyButton()
        {
            bool hasSelection = modeList.SelectedItems.Count > 0;
            applyButton.Enabled = hasSelection;
            if (!hasSelection)
            {
                applyButton.Text = "选择一个模式";
                applyButton.AccessibleName = "请先从列表中选择一个显示模式";
                return;
            }

            DisplayMode selected = (DisplayMode)modeList.SelectedItems[0].Tag;
            applyButton.Text = IsSameMode(selected, currentMode)
                ? "当前已经是此模式"
                : "切换到 " + selected;
            applyButton.AccessibleName = applyButton.Text;
        }

        private void ApplySelectedMode()
        {
            if (modeList.SelectedItems.Count == 0)
            {
                statusLabel.ForeColor = Color.FromArgb(176, 96, 24);
                statusLabel.SetAccessibleText("请先从列表中选择一个显示模式。", true);
                return;
            }

            DisplayMode selected = (DisplayMode)modeList.SelectedItems[0].Tag;
            ApplyMode(selected);
        }

        private void ApplyMode(DisplayMode mode)
        {
            SetControlsEnabled(false);
            Cursor = Cursors.WaitCursor;
            statusLabel.ForeColor = Color.FromArgb(88, 99, 111);
            statusLabel.SetAccessibleText("正在切换…屏幕可能会短暂闪黑", true);
            statusLabel.Refresh();

            SwitchResult result = DisplayManager.SwitchTo(mode.Width, mode.Height, mode.RefreshRate);

            Cursor = Cursors.Default;
            SetControlsEnabled(true);

            if (!result.Success)
            {
                ShowFailure(result.Message);
                return;
            }

            RefreshCurrentMode();
            statusLabel.ForeColor = Color.FromArgb(24, 115, 72);
            statusLabel.SetAccessibleText(result.Message, true);
        }

        private void RefreshCurrentMode()
        {
            try
            {
                DisplayMode latest = DisplayManager.GetCurrentMode();
                if (!IsSameMode(latest, currentMode))
                {
                    currentMode = latest;
                    UpdateCurrentModeLabel(true);
                    PopulateModeList();
                }
            }
            catch (Exception ex)
            {
                currentModeLabel.SetAccessibleText("当前分辨率：读取失败", true);
                statusLabel.ForeColor = Color.FromArgb(176, 44, 44);
                statusLabel.SetAccessibleText(ex.Message, true);
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            modeList.Enabled = enabled;
            currentRefreshOnlyCheckBox.Enabled = enabled;
            refreshButton.Enabled = enabled;
            applyButton.Enabled = enabled && modeList.SelectedItems.Count > 0;
        }

        private void OnModeListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ApplySelectedMode();
            }
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                e.SuppressKeyPress = true;
                ReloadModes(true);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        private void ShowFailure(string message)
        {
            statusLabel.ForeColor = Color.FromArgb(176, 44, 44);
            statusLabel.SetAccessibleText(message, true);
            MessageBox.Show(
                this,
                message,
                "分辨率切换失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    internal static class Program
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
