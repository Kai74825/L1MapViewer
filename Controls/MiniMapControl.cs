using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using L1MapViewer.Models;
using L1MapViewer.Rendering;

namespace L1MapViewer.Controls
{
    /// <summary>
    /// 小地圖控件 - 顯示整張地圖的縮圖並支援導航
    /// </summary>
    public class MiniMapControl : UserControl
    {
        #region 私有欄位

        private PictureBox _pictureBox;
        private readonly MapRenderingCore _renderingCore;
        private Bitmap _miniMapBitmap;

        private bool _isDragging;
        private MapDocument _document;

        #endregion

        #region 公開屬性

        /// <summary>
        /// 關聯的 MapViewerControl
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MapViewerControl MapViewer { get; set; }

        /// <summary>
        /// 關聯的 ViewState
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ViewState ViewState { get; set; }

        /// <summary>
        /// 小地圖大小
        /// </summary>
        [DefaultValue(200)]
        public int MiniMapSize { get; set; } = 200;

        /// <summary>
        /// 視窗框顏色
        /// </summary>
        [DefaultValue(typeof(Color), "Red")]
        public Color ViewportRectColor { get; set; } = Color.Red;

        /// <summary>
        /// 視窗框寬度
        /// </summary>
        [DefaultValue(2f)]
        public float ViewportRectWidth { get; set; } = 2f;

        #endregion

        #region 事件

        /// <summary>
        /// 導航請求事件（點擊/拖曳時）
        /// </summary>
        public event EventHandler<Point> NavigateRequested;

        #endregion

        #region 建構函式

        public MiniMapControl()
        {
            _renderingCore = new MapRenderingCore();
            InitializeComponents();
        }

        public MiniMapControl(MapViewerControl mapViewer) : this()
        {
            MapViewer = mapViewer;
            if (mapViewer != null)
            {
                ViewState = mapViewer.ViewState;
            }
        }

        #endregion

        #region 初始化

        private void InitializeComponents()
        {
            this.Size = new Size(MiniMapSize, MiniMapSize);
            this.BackColor = Color.Black;

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            _pictureBox.MouseDown += PictureBox_MouseDown;
            _pictureBox.MouseMove += PictureBox_MouseMove;
            _pictureBox.MouseUp += PictureBox_MouseUp;
            _pictureBox.Paint += PictureBox_Paint;

            this.Controls.Add(_pictureBox);
        }

        #endregion

        #region 公開方法

        /// <summary>
        /// 更新小地圖
        /// </summary>
        public void UpdateMiniMap(MapDocument document)
        {
            _document = document;
            if (document == null) return;

            var bitmap = _renderingCore.RenderMiniMap(document, MiniMapSize);

            _miniMapBitmap?.Dispose();
            _miniMapBitmap = bitmap;
            _pictureBox.Invalidate();
        }

        /// <summary>
        /// 重繪視窗位置框
        /// </summary>
        public void RefreshViewportRect()
        {
            _pictureBox.Invalidate();
        }

        #endregion

        #region 滑鼠事件

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                NavigateToPosition(e.Location);
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                NavigateToPosition(e.Location);
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = false;
            }
        }

        private void NavigateToPosition(Point mouseLocation)
        {
            if (ViewState == null || _document == null) return;

            // 計算小地圖的實際顯示區域
            float scaleX = (float)this.Width / ViewState.MapWidth;
            float scaleY = (float)this.Height / ViewState.MapHeight;
            float scale = Math.Min(scaleX, scaleY);

            int scaledWidth = (int)(ViewState.MapWidth * scale);
            int scaledHeight = (int)(ViewState.MapHeight * scale);

            int offsetX = (this.Width - scaledWidth) / 2;
            int offsetY = (this.Height - scaledHeight) / 2;

            // 轉換為世界座標
            int worldX = (int)((mouseLocation.X - offsetX) / scale);
            int worldY = (int)((mouseLocation.Y - offsetY) / scale);

            // 限制在有效範圍內
            worldX = Math.Max(0, Math.Min(ViewState.MapWidth, worldX));
            worldY = Math.Max(0, Math.Min(ViewState.MapHeight, worldY));

            // 觸發導航事件
            NavigateRequested?.Invoke(this, new Point(worldX, worldY));

            // 如果有關聯的 MapViewer，直接導航
            MapViewer?.ScrollTo(worldX, worldY);
        }

        #endregion

        #region 繪製

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            if (_miniMapBitmap != null)
            {
                // 計算置中繪製位置
                float scaleX = (float)this.Width / _miniMapBitmap.Width;
                float scaleY = (float)this.Height / _miniMapBitmap.Height;
                float scale = Math.Min(scaleX, scaleY);

                int drawWidth = (int)(_miniMapBitmap.Width * scale);
                int drawHeight = (int)(_miniMapBitmap.Height * scale);
                int drawX = (this.Width - drawWidth) / 2;
                int drawY = (this.Height - drawHeight) / 2;

                e.Graphics.DrawImage(_miniMapBitmap, drawX, drawY, drawWidth, drawHeight);
            }

            // 繪製視窗位置紅框
            if (ViewState != null && ViewState.MapWidth > 0)
            {
                DrawViewportRect(e.Graphics);
            }
        }

        private void DrawViewportRect(Graphics g)
        {
            // 計算縮放比例
            float scaleX = (float)this.Width / ViewState.MapWidth;
            float scaleY = (float)this.Height / ViewState.MapHeight;
            float scale = Math.Min(scaleX, scaleY);

            int scaledWidth = (int)(ViewState.MapWidth * scale);
            int scaledHeight = (int)(ViewState.MapHeight * scale);

            int offsetX = (this.Width - scaledWidth) / 2;
            int offsetY = (this.Height - scaledHeight) / 2;

            // 計算視窗在小地圖上的位置
            int rectX = offsetX + (int)(ViewState.ScrollX * scale);
            int rectY = offsetY + (int)(ViewState.ScrollY * scale);
            int rectW = (int)(ViewState.ViewportWidth / ViewState.ZoomLevel * scale);
            int rectH = (int)(ViewState.ViewportHeight / ViewState.ZoomLevel * scale);

            // 限制在小地圖範圍內
            rectX = Math.Max(offsetX, rectX);
            rectY = Math.Max(offsetY, rectY);
            rectW = Math.Min(scaledWidth - (rectX - offsetX), rectW);
            rectH = Math.Min(scaledHeight - (rectY - offsetY), rectH);

            if (rectW > 0 && rectH > 0)
            {
                using (var pen = new Pen(ViewportRectColor, ViewportRectWidth))
                {
                    g.DrawRectangle(pen, rectX, rectY, rectW, rectH);
                }
            }
        }

        #endregion

        #region Dispose

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _miniMapBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
