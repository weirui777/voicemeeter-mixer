using System;
using System.Drawing;
using System.Windows.Forms;

namespace VMixer
{
    /// <summary>
    /// 支持拖动排序的通道列表。
    ///
    /// WinForms 的 ListView 没有原生的拖动排序，这里自己实现：
    /// 鼠标左键在行上按下并移动超过阈值后开始拖动，
    /// 拖动时在列表上方画一条蓝色插入线，松开鼠标后把行真正挪到新位置。
    ///
    /// 勾选框那一小块不参与拖动（否则点勾选框会误触发排序）。
    /// </summary>
    public class DragListView : ListView
    {
        // 拖动状态
        private int _startIndex = -1;      // 正在拖的行（-1 = 没在拖）
        private int _dropIndex = -1;       // 目标插入位置（移除后的下标）
        private Point _startPoint;
        private bool _dragging;

        /// <summary>插入指示线（由父窗口创建并浮在列表上面）。</summary>
        public Panel Indicator { get; set; }

        // 临时调试日志
        private static readonly bool DbgEnabled =
            Environment.GetEnvironmentVariable("VMIXER_DRAG_DEBUG") == "1";

        internal static void Dbg(string s)
        {
            if (!DbgEnabled) return;
            try
            {
                string path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "vmixer_drag.log");
                System.IO.File.AppendAllText(path,
                    System.DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n");
            }
            catch { }
        }

        /// <summary>行顺序发生变化时触发（父窗口据此同步自己的顺序表）。</summary>
        public event EventHandler OrderChanged;

        public DragListView()
        {
            View = View.Details;
            FullRowSelect = true;
            CheckBoxes = true;
            HideSelection = false;
            MultiSelect = false;
            GridLines = true;
            Sorting = SortOrder.None;
            AllowDrop = false;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Dbg("Down x=" + e.X + " y=" + e.Y + " btn=" + e.Button + " count=" + Items.Count);
            if (e.Button != MouseButtons.Left) { ResetDrag(); return; }

            ResetDrag();
            int idx = HitIndex(e.X, e.Y);
            Dbg("  hit=" + idx);
            if (idx < 0) return;

            // 勾选框那一小块不启动拖动（勾选框画在行最左侧，图标/文字区域之前）
            try
            {
                Rectangle icon = Items[idx].GetBounds(ItemBoundsPortion.Icon);
                Dbg("  iconRect=" + icon.X + "," + icon.Y + "," + icon.Width + "," + icon.Height);
                if (icon.X > 0 && e.X < icon.X) { Dbg("  in checkbox -> no drag"); return; }
            }
            catch (Exception ex) { Dbg("  iconErr=" + ex.Message); }

            _startIndex = idx;
            _startPoint = e.Location;
            Dbg("  startIndex=" + _startIndex);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_startIndex < 0) return;
            if ((e.Button & MouseButtons.Left) == 0) { ResetDrag(); return; }

            if (!_dragging)
            {
                int dx = e.X - _startPoint.X;
                int dy = e.Y - _startPoint.Y;
                // 必须取绝对值：向上拖时 dy 是负的，否则永远达不到阈值
                if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4) return;    // 抖动不算拖动
                _dragging = true;
                Dbg("Move start drag dx=" + dx + " dy=" + dy);
                try { Capture = true; } catch { }
            }

            int lineY;
            int target = ComputeDropIndex(e.Y, out lineY);
            Dbg("Move y=" + e.Y + " drop=" + target + " lineY=" + lineY);
            if (target != _dropIndex)
            {
                _dropIndex = target;
                ShowIndicator(lineY);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Dbg("Up x=" + e.X + " y=" + e.Y + " dragging=" + _dragging + " from=" + _startIndex + " to=" + _dropIndex);
            if (_dragging && _dropIndex >= 0 && _startIndex >= 0)
            {
                ApplyReorder(_startIndex, _dropIndex);
            }
            ResetDrag();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_dragging && (Control.MouseButtons & MouseButtons.Left) == 0) ResetDrag();
        }

        private void ResetDrag()
        {
            _startIndex = -1;
            _dropIndex = -1;
            _dragging = false;
            try { Capture = false; } catch { }
            HideIndicator();
        }

        /// <summary>按下时命中的行下标（-1 = 没命中）。</summary>
        private int HitIndex(int x, int y)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                int top, bottom;
                RowBounds(i, out top, out bottom);
                if (bottom <= top) continue;
                if (y >= top && y < bottom) return i;
            }
            return -1;
        }

        /// <summary>某个整行的纵向范围（上下边界）。</summary>
        private void RowBounds(int index, out int top, out int bottom)
        {
            top = 0;
            bottom = 0;
            if (index < 0 || index >= Items.Count) return;
            try
            {
                Rectangle r = GetItemRect(index);
                top = r.Top;
                bottom = r.Bottom;
            }
            catch { }
            if (bottom <= top)
            {
                // 兜底：用控件高度粗略估计
                top = index * 18;
                bottom = top + 18;
            }
        }

        /// <summary>
        /// 根据鼠标纵坐标算出「插到第几个位置」，同时给出插入线的 y 坐标。
        /// </summary>
        private int ComputeDropIndex(int y, out int lineY)
        {
            lineY = 0;
            int count = Items.Count;
            if (count == 0) return 0;

            int j = -1;
            for (int i = 0; i < count; i++)
            {
                int top, bottom;
                RowBounds(i, out top, out bottom);
                if (bottom <= top) continue;
                if (y >= top && y < bottom) { j = i; break; }
            }

            int origInsert;      // 原始列表里的插入点（插在这个下标之前）
            if (j < 0)
            {
                int firstTop, firstBottom, lastTop, lastBottom;
                RowBounds(0, out firstTop, out firstBottom);
                RowBounds(count - 1, out lastTop, out lastBottom);
                if (y < firstTop) origInsert = 0;
                else origInsert = count;
            }            else
            {
                int top, bottom;
                RowBounds(j, out top, out bottom);
                bool below = y > top + (bottom - top) / 2;
                origInsert = below ? j + 1 : j;
            }

            // 插入线的位置
            if (origInsert >= count)
            {
                int t, b;
                RowBounds(count - 1, out t, out b);
                lineY = b;
            }
            else
            {
                int t, b;
                RowBounds(origInsert, out t, out b);
                lineY = t;
            }

            // 换算成「把被拖的行拿掉之后」的插入下标
            int reduced = origInsert > _startIndex ? origInsert - 1 : origInsert;
            if (reduced < 0) reduced = 0;
            if (reduced > count - 1) reduced = count - 1;
            return reduced;
        }

        /// <summary>
        /// 自测用：用合成的鼠标事件走一遍完整拖动流程
        /// （按下 → 移动 → 松开），跟真实鼠标走的是同一套代码。
        /// 自动化测试环境（锁屏 / 无交互桌面）里没法真的挪鼠标，用它来验证逻辑。
        /// </summary>
        public bool SimulateDrag(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= Items.Count) return false;
            if (toIndex < 0 || toIndex >= Items.Count) return false;

            Rectangle from = GetItemRect(fromIndex);
            Rectangle to = GetItemRect(toIndex);
            int sx = 60;                                   // 文本列，避开勾选框
            int sy = from.Top + (from.Bottom - from.Top) / 2;
            // 往下拖 -> 放到目标行下面；往上拖 -> 放到目标行上面
            int ty = toIndex > fromIndex ? to.Bottom - 2 : to.Top;

            Dbg("SimulateDrag " + fromIndex + " -> " + toIndex + " sy=" + sy + " ty=" + ty);
            OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, sx, sy, 0));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, sx, (sy + ty) / 2, 0));
            OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, sx, ty, 0));
            OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, sx, ty, 0));
            return true;
        }

        /// <summary>把行真正挪到新位置。</summary>
        private void ApplyReorder(int from, int to)        {
            if (from < 0 || from >= Items.Count) return;
            if (to < 0 || to >= Items.Count) return;
            if (from == to) return;

            ListViewItem moved = Items[from];
            Items.RemoveAt(from);
            Items.Insert(to, moved);
            moved.Selected = true;
            try { moved.EnsureVisible(); } catch { }

            if (OrderChanged != null) OrderChanged(this, EventArgs.Empty);
        }

        private void ShowIndicator(int listY)
        {
            if (Indicator == null) return;
            Control parent = Parent;
            if (parent == null) return;
            try
            {
                Point p = PointToScreen(new Point(0, listY));
                p = parent.PointToClient(p);
                Indicator.Location = new Point(Left + Ui.S(2), p.Y - Indicator.Height / 2);
                Indicator.Width = Math.Max(20, Width - Ui.S(4));
                Indicator.Visible = true;
                Indicator.BringToFront();
            }
            catch { }
        }

        private void HideIndicator()
        {
            if (Indicator == null) return;
            try { Indicator.Visible = false; } catch { }
        }
    }
}
