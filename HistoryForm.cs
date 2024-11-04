using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AruScreenSummary;

namespace AruScreenSummary
{
    public class HistoryForm : Form
    {
        private ListView listView;
        private const int MAX_CONTENT_LENGTH = 100;  // 内容预览的最大长度

        public HistoryForm()
        {
            this.Text = "历史记录";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 创建ListView
            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };

            // 添加列
            listView.Columns.Add("时间", 150);
            listView.Columns.Add("内容预览", 300);
            listView.Columns.Add("Prompt Tokens", 100);  // 新增
            listView.Columns.Add("Completion Tokens", 100);  // 新增
            listView.Columns.Add("Total Tokens", 100);  // 新增

            // 加载历史记录
            LoadHistory();

            // 双击事件处理
            listView.DoubleClick += ListView_DoubleClick;

            this.Controls.Add(listView);
        }

        private void LoadHistory()
        {
            listView.Items.Clear();
            var history = typeof(TranslationHistory).GetField("_history",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)?.GetValue(null) as List<TranslationRecord>;

            if (history != null)
            {
                foreach (var record in history)
                {
                    var item = new ListViewItem(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));

                    // 截断过长的内容
                    string preview = record.Content;
                    if (preview.Length > MAX_CONTENT_LENGTH)
                    {
                        preview = preview.Substring(0, MAX_CONTENT_LENGTH) + "...";
                    }

                    item.SubItems.Add(preview);
                    item.SubItems.Add(record.PromptTokens.ToString());  // Prompt Tokens
                    item.SubItems.Add(record.CompletionTokens.ToString());  // Completion Tokens
                    item.SubItems.Add(record.TotalTokens.ToString());  // Total Tokens

                    // 存储完整内容
                    item.Tag = record;

                    listView.Items.Add(item);
                }
            }
        }

        private void ListView_DoubleClick(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                var record = listView.SelectedItems[0].Tag as TranslationRecord;
                if (record != null)
                {
                    var detailForm = new HistoryDetailForm(record);
                    detailForm.ShowDialog();
                }
            }
        }
    }

    public class HistoryDetailForm : Form
    {
        public HistoryDetailForm(TranslationRecord record)
        {
            this.Text = "详细信息";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(10)
            };

            // 添加时间信息
            layout.Controls.Add(new Label { Text = "时间:", AutoSize = true }, 0, 0);
            layout.Controls.Add(new Label { Text = record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), AutoSize = true }, 1, 0);

            // 添加Token信息
            layout.Controls.Add(new Label { Text = "Prompt Tokens:", AutoSize = true }, 0, 1);
            layout.Controls.Add(new Label { Text = record.PromptTokens.ToString(), AutoSize = true }, 1, 1);

            layout.Controls.Add(new Label { Text = "Completion Tokens:", AutoSize = true }, 0, 2);
            layout.Controls.Add(new Label { Text = record.CompletionTokens.ToString(), AutoSize = true }, 1, 2);

            layout.Controls.Add(new Label { Text = "Total Tokens:", AutoSize = true }, 0, 3);
            layout.Controls.Add(new Label { Text = record.TotalTokens.ToString(), AutoSize = true }, 1, 3);

            // 添加内容
            layout.Controls.Add(new Label { Text = "内容:", AutoSize = true }, 0, 4);

            TextBox contentBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = record.Content
            };

            layout.Controls.Add(contentBox, 1, 4);
            layout.SetRowSpan(contentBox, 1);

            // 设置行列样式
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int i = 0; i < 4; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this.Controls.Add(layout);
        }
    }
}
