using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using AnimalWinForms.Services; 

namespace AnimalWinForms
{
    public sealed class Form1 : Form
    {
        // UI
        private PictureBox _picture;
        private Button _btnOpen, _btnCheck, _btnClear, _btnSort;
        private CheckBox _chkAuto;
        private Label _lblResult, _lblTop3, _lblInfo;

        // ML
        private readonly AnimalClassifier _classifier;

        public Form1()
        {
            Text = "Animal Recognition";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 560);

            BuildUi();
            WireShortcuts();
            WireDragDrop();

            _classifier = new AnimalClassifier();
            _lblInfo.Text = $"�������: {string.Join(", ", _classifier.Labels)}";
        }

        #region UI

        private void BuildUi()
        {
            // ����� � 3 ����: ������ | ��� | ���������
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // ������
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // ���
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // ���������
            Controls.Add(root);

            // ������ (��� ����� �����)
            _picture = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            root.Controls.Add(_picture, 0, 0);

            //��� � ��-������ ������ + ����
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(10, 10, 10, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var btnFont = new Font("Segoe UI", 10f, FontStyle.Regular);
            var btnSize = new Size(180, 40); 

            _btnOpen = new Button { Text = "������ �����������", Font = btnFont, Size = btnSize };
            _btnOpen.Click += async (_, __) => await OpenImageAsync();
            bar.Controls.Add(_btnOpen);

            _btnSort = new Button { Text = "�������� �����", Font = btnFont, Size = btnSize };
            _btnSort.Click += async (_, __) => await SortFolderAsync();
            bar.Controls.Add(_btnSort);

            _btnCheck = new Button { Text = "�������", Font = btnFont, Size = btnSize, Enabled = false };
            _btnCheck.Click += async (_, __) => await EvaluateAsync();
            bar.Controls.Add(_btnCheck);

            _btnClear = new Button { Text = "�������", Font = btnFont, Size = btnSize, Enabled = false };
            _btnClear.Click += (_, __) => ClearImage();
            bar.Controls.Add(_btnClear);

            _chkAuto = new CheckBox { Text = "����", Checked = true, AutoSize = true, Margin = new Padding(12, 12, 6, 0) };
            bar.Controls.Add(_chkAuto);

            root.Controls.Add(bar, 0, 1);

            // ��������� (Top-1 + Top-3 ���� + ����)
            var results = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 6, 12, 12),
                ColumnCount = 1,
            };

            _lblResult = new Label
            {
                Text = "��������:",
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 2, 0, 2),
            };
            results.Controls.Add(_lblResult, 0, 0);

            _lblTop3 = new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 4),
                Text = "" 
            };
            results.Controls.Add(_lblTop3, 0, 1);

            _lblInfo = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 2),
            };
            results.Controls.Add(_lblInfo, 0, 2);

            root.Controls.Add(results, 0, 2);
        }

        private void WireShortcuts()
        {
            KeyPreview = true;
            KeyDown += async (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.O) { await OpenImageAsync(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter && _btnCheck.Enabled) { await EvaluateAsync(); e.Handled = true; }
                else if (e.KeyCode == Keys.Delete && _btnClear.Enabled) { ClearImage(); e.Handled = true; }
            };

            var tip = new ToolTip();
            tip.SetToolTip(_btnOpen, "Ctrl+O");
            tip.SetToolTip(_btnCheck, "Enter");
            tip.SetToolTip(_btnClear, "Del");
            tip.SetToolTip(_btnSort, "����������� ������ ����������� � ������� �����");
        }

        private void WireDragDrop()
        {
            AllowDrop = true;
            DragEnter += (s, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files?.Any(IsImageFile) == true) e.Effect = DragDropEffects.Copy;
                }
            };
            DragDrop += async (s, e) =>
            {
                var files = (string[])e.Data!.GetData(DataFormats.FileDrop);
                var firstImg = files?.FirstOrDefault(IsImageFile);
                if (firstImg != null)
                {
                    LoadImage(firstImg);
                    if (_chkAuto.Checked) await EvaluateAsync();
                }
            };
        }

        #endregion

        #region Actions

        private async Task OpenImageAsync()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "�������� �����������",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            LoadImage(ofd.FileName);
            if (_chkAuto.Checked) await EvaluateAsync();
        }

        private void LoadImage(string path)
        {
            try
            {
                _picture.Image?.Dispose();
                _picture.Image = Image.FromFile(path);
                _picture.Tag = path;

                _btnCheck.Enabled = true;
                _btnClear.Enabled = true;

                _lblResult.Text = "��������:";
                _lblTop3.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"��������� ��������� �� �������������.\n\n{ex.Message}", "������",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearImage()
        {
            _picture.Image?.Dispose();
            _picture.Image = null;
            _picture.Tag = null;
            _btnCheck.Enabled = false;
            _btnClear.Enabled = false;

            _lblResult.Text = "��������:";
            _lblTop3.Text = "";
        }

        private async Task EvaluateAsync()
        {
            var path = _picture.Tag as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show("����� �������� �����������.", "��������",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var top = await Task.Run(() => _classifier.PredictTop(path, 5).ToList());

                if (top.Count == 0)
                {
                    _lblResult.Text = "���� ��������.";
                    _lblTop3.Text = "";
                    return;
                }

                var best = top.First();
                _lblResult.Text = $"Top-1: {best.label} ({best.prob:P1})";

                // ���-3 ���� (�� ������� �� ������ Top-K)
                var top3 = top.Take(3).Select(x => $"{x.label} ({x.prob:P1})");
                _lblTop3.Text = "Top-3: " + string.Join(" | ", top3);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "�������� ������ ��� ��������������.\n\n" +
                    "��� ������� ������ �� �������� ������ \"Feature\" ��� \"Image\":\n" +
                    "� ������� ��, �� WinForms �������� ������ ��������� .zip ����� �� �������.\n" +
                    "� ��������� bin/obj � ���������������.\n\n" +
                    $"������:\n{ex.Message}",
                    "������", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SortFolderAsync()
        {
            using var fbd = new FolderBrowserDialog
            {
                Description = "�������� ����� ��� ������ �� ���������",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            if (fbd.ShowDialog() != DialogResult.OK) return;

            var srcRoot = fbd.SelectedPath;
            if (string.IsNullOrWhiteSpace(srcRoot) || !Directory.Exists(srcRoot))
            {
                MessageBox.Show("��������� �����.", "������", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var dstRoot = srcRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + "_sorted";
            Directory.CreateDirectory(dstRoot);

            var files = Directory
                .EnumerateFiles(srcRoot, "*.*", SearchOption.AllDirectories)
                .Where(IsImageFile)
                .ToArray();

            if (files.Length == 0)
            {
                MessageBox.Show("�� ������ ����������� � ���� �����.", "����������",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var perLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int errors = 0;

            // ������� ������ ��� ����� �����
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        var top1 = _classifier.PredictTop(file, 1).FirstOrDefault();
                        var label = string.IsNullOrWhiteSpace(top1.label) ? "unknown" : top1.label;

                        var labelDir = Path.Combine(dstRoot, Sanitize(label));
                        Directory.CreateDirectory(labelDir);

                        var dstPath = Path.Combine(labelDir, Path.GetFileName(file));
                        dstPath = EnsureUniquePath(dstPath);

                        File.Copy(file, dstPath, overwrite: false); 

                        if (!perLabel.TryGetValue(label, out var cnt)) cnt = 0;
                        perLabel[label] = cnt + 1;
                    }
                    catch
                    {
                        errors++;
                    }
                }
            });

            var summary = string.Join(Environment.NewLine, perLabel
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key}: {kv.Value}"));

            MessageBox.Show(
                $"������! �������� �������: {perLabel.Values.Sum()} �� {files.Length}\n" +
                (errors > 0 ? $"������: {errors}\n" : "") +
                $"�����: {dstRoot}\n\n�������������:\n{summary}",
                "��������� ���������",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ��������� ��� �� �����
        private static string Sanitize(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars())
                name = name.Replace(ch, '_');
            return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
        }

        // ������ (1), (2) ... ��� ������ ����������
        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            int i = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(dir, $"{name} ({i++}){ext}");
            } while (File.Exists(candidate));
            return candidate;
        }

        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".bmp";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _classifier.Dispose();
            base.OnFormClosed(e);
        }

        #endregion
    }
}
