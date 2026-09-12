using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Cajolote.Views
{
    public partial class ImageCropWindow : Window
    {
        private string _originalImagePath = string.Empty;
        private double _imgOriginalWidth;
        private double _imgOriginalHeight;
        private double _fitScale;

        private Point _startPoint;
        private Point _origin;
        private bool _isDragging;

        public string? CroppedImagePath { get; private set; }

        public ImageCropWindow(string imagePath)
        {
            InitializeComponent();
            LoadImage(imagePath);
        }

        private void LoadImage(string imagePath)
        {
            try
            {
                _originalImagePath = imagePath;

                // Load bitmap using WPF to show in UI
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                _imgOriginalWidth = bitmap.PixelWidth;
                _imgOriginalHeight = bitmap.PixelHeight;

                SourceImage.Source = bitmap;

                // Calculate uniform fit size inside the 300x300 container
                double containerSize = 300;
                double scaleX = containerSize / _imgOriginalWidth;
                double scaleY = containerSize / _imgOriginalHeight;
                _fitScale = Math.Min(scaleX, scaleY);

                double fitWidth = _imgOriginalWidth * _fitScale;
                double fitHeight = _imgOriginalHeight * _fitScale;

                SourceImage.Width = fitWidth;
                SourceImage.Height = fitHeight;

                // Minimum scale is what makes the smaller dimension of the image match the crop size (220)
                double minScale = Math.Max(220 / fitWidth, 220 / fitHeight);
                ZoomSlider.Minimum = minScale;
                ZoomSlider.Maximum = Math.Max(minScale * 5.0, 5.0);
                ZoomSlider.Value = minScale;

                // Initialize scale transform
                ImgScale.ScaleX = minScale;
                ImgScale.ScaleY = minScale;
                ImgScale.CenterX = 150;
                ImgScale.CenterY = 150;

                // Center the image initially using translation at minScale
                ImgTranslate.X = minScale * (150 - fitWidth / 2);
                ImgTranslate.Y = minScale * (150 - fitHeight / 2);

                ClampTranslation();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar la imagen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void ClampTranslation()
        {
            if (SourceImage == null || ImgScale == null || ImgTranslate == null) return;

            double scale = ImgScale.ScaleX;

            // Circle center = 150, 150. Radius = 110. Bounds: [40, 260]. Width/Height = 220.
            // Crop boundaries: CropLeft = 40, CropRight = 260, CropTop = 40, CropBottom = 260.
            // LeftTransformed = -150 * scale + 150 + TranslateX. We want LeftTransformed <= 40
            // TranslateX <= -110 + 150 * scale
            double maxX = -110 + 150 * scale;

            // RightTransformed = (Width - 150) * scale + 150 + TranslateX. We want RightTransformed >= 260
            // TranslateX >= 110 - (Width - 150) * scale
            double minX = 110 - (SourceImage.Width - 150) * scale;

            // TopTransformed = -150 * scale + 150 + TranslateY. We want TopTransformed <= 40
            // TranslateY <= -110 + 150 * scale
            double maxY = -110 + 150 * scale;

            // BottomTransformed = (Height - 150) * scale + 150 + TranslateY. We want BottomTransformed >= 260
            // TranslateY >= 110 - (SourceImage.Height - 150) * scale
            double minY = 110 - (SourceImage.Height - 150) * scale;

            // Clamp values
            ImgTranslate.X = Clamp(ImgTranslate.X, minX, maxX);
            ImgTranslate.Y = Clamp(ImgTranslate.Y, minY, maxY);
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the entire window by clicking on the background/border
            if (e.Source == this || e.Source is System.Windows.Controls.Border)
            {
                DragMove();
            }
        }

        private void CanvasContainer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(CanvasContainer);
            _origin = new Point(ImgTranslate.X, ImgTranslate.Y);
            CanvasContainer.CaptureMouse();
        }

        private void CanvasContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetPosition(CanvasContainer);
            Vector diff = currentPoint - _startPoint;

            ImgTranslate.X = _origin.X + diff.X;
            ImgTranslate.Y = _origin.Y + diff.Y;

            ClampTranslation();
        }

        private void CanvasContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                CanvasContainer.ReleaseMouseCapture();
            }
        }

        private void CanvasContainer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomChange = e.Delta > 0 ? 0.1 : -0.1;
            double newZoom = ZoomSlider.Value + zoomChange;
            if (newZoom >= ZoomSlider.Minimum && newZoom <= ZoomSlider.Maximum)
            {
                ZoomSlider.Value = newZoom;
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ImgScale != null)
            {
                ImgScale.ScaleX = e.NewValue;
                ImgScale.ScaleY = e.NewValue;

                // Center of scaling is at the center of the crop circle (150, 150)
                ImgScale.CenterX = 150;
                ImgScale.CenterY = 150;

                ClampTranslation();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Crop coordinates relative to CanvasContainer:
                // Circle center = 150, 150. Radius = 110. Bounding box = (40, 40) to (260, 260), width/height = 220.
                double cropSize = 220;
                double cropOffset = 40;

                // P_img = Center + (P_container - Translation - Center) / Scale
                double leftImg = 150.0 + (cropOffset - ImgTranslate.X - 150.0) / ImgScale.ScaleX;
                double topImg = 150.0 + (cropOffset - ImgTranslate.Y - 150.0) / ImgScale.ScaleY;
                double widthImg = cropSize / ImgScale.ScaleX;
                double heightImg = cropSize / ImgScale.ScaleY;

                // P_orig = P_img / fitScale
                double leftOrig = leftImg / _fitScale;
                double topOrig = topImg / _fitScale;
                double widthOrig = widthImg / _fitScale;
                double heightOrig = heightImg / _fitScale;

                // Clamp to original image bounds
                if (leftOrig < 0) { widthOrig += leftOrig; leftOrig = 0; }
                if (topOrig < 0) { heightOrig += topOrig; topOrig = 0; }
                if (leftOrig + widthOrig > _imgOriginalWidth) widthOrig = _imgOriginalWidth - leftOrig;
                if (topOrig + heightOrig > _imgOriginalHeight) heightOrig = _imgOriginalHeight - topOrig;

                // Handle degenerate cases
                if (widthOrig <= 0 || heightOrig <= 0)
                {
                    MessageBox.Show("El área seleccionada no es válida. Por favor, ajusta la imagen de nuevo.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Process with SkiaSharp
                using (var originalBitmap = LoadBitmapWithOrientation(_originalImagePath))
                {
                    if (originalBitmap == null)
                    {
                        throw new Exception("No se pudo cargar la imagen original.");
                    }

                    // Create destination bitmap (512x512 pixels for high resolution but compact size)
                    using (var croppedBitmap = new SKBitmap(512, 512))
                    {
                        using (var canvas = new SKCanvas(croppedBitmap))
                        {
                            canvas.Clear(SKColors.Transparent);

                            var srcRect = new SKRect((float)leftOrig, (float)topOrig, (float)(leftOrig + widthOrig), (float)(topOrig + heightOrig));
                            var destRect = new SKRect(0, 0, 512, 512);

                            using (var paint = new SKPaint
                            {
#pragma warning disable CS0618
                                FilterQuality = SKFilterQuality.High,
#pragma warning restore CS0618
                                IsAntialias = true
                            })
                            {
                                canvas.DrawBitmap(originalBitmap, srcRect, destRect, paint);
                            }
                        }

                        // Save cropped image as PNG in App Temp directory
                        var tempPath = Path.Combine(Path.GetTempPath(), $"cropped_{Guid.NewGuid()}.png");
                        using (var image = SKImage.FromBitmap(croppedBitmap))
                        using (var data = image.Encode(SKEncodedImageFormat.Png, 90))
                        using (var stream = File.OpenWrite(tempPath))
                        {
                            data.SaveTo(stream);
                        }

                        CroppedImagePath = tempPath;
                        DialogResult = true;
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al recortar la imagen con SkiaSharp: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads SKBitmap while respecting EXIF orientation tags.
        /// </summary>
        private static SKBitmap LoadBitmapWithOrientation(string path)
        {
            try
            {
                using (var stream = new SKFileStream(path))
                using (var codec = SKCodec.Create(stream))
                {
                    if (codec == null) return SKBitmap.Decode(path);

                    var bitmap = SKBitmap.Decode(codec);
                    var origin = codec.EncodedOrigin;

                    if (origin == SKEncodedOrigin.TopLeft)
                    {
                        return bitmap;
                    }

                    SKBitmap rotated;
                    switch (origin)
                    {
                        case SKEncodedOrigin.TopRight:
                            rotated = Flip(bitmap, true, false);
                            break;
                        case SKEncodedOrigin.BottomRight:
                            rotated = Rotate(bitmap, 180);
                            break;
                        case SKEncodedOrigin.BottomLeft:
                            rotated = Flip(bitmap, false, true);
                            break;
                        case SKEncodedOrigin.LeftTop:
                            rotated = Rotate(bitmap, 90);
                            rotated = Flip(rotated, true, false);
                            break;
                        case SKEncodedOrigin.RightTop:
                            rotated = Rotate(bitmap, 90);
                            break;
                        case SKEncodedOrigin.RightBottom:
                            rotated = Rotate(bitmap, 90);
                            rotated = Flip(rotated, false, true);
                            break;
                        case SKEncodedOrigin.LeftBottom:
                            rotated = Rotate(bitmap, 270);
                            break;
                        default:
                            return bitmap;
                    }

                    bitmap.Dispose();
                    return rotated;
                }
            }
            catch
            {
                return SKBitmap.Decode(path);
            }
        }

        private static SKBitmap Rotate(SKBitmap bitmap, double angle)
        {
            var radians = Math.PI * angle / 180.0;
            var sin = Math.Abs(Math.Sin(radians));
            var cos = Math.Abs(Math.Cos(radians));
            var newWidth = (int)Math.Round(bitmap.Width * cos + bitmap.Height * sin);
            var newHeight = (int)Math.Round(bitmap.Width * sin + bitmap.Height * cos);

            var rotated = new SKBitmap(newWidth, newHeight);
            using (var canvas = new SKCanvas(rotated))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(newWidth / 2f, newHeight / 2f);
                canvas.RotateDegrees((float)angle);
                canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            return rotated;
        }

        private static SKBitmap Flip(SKBitmap bitmap, bool horizontal, bool vertical)
        {
            var flipped = new SKBitmap(bitmap.Width, bitmap.Height);
            using (var canvas = new SKCanvas(flipped))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(horizontal ? -1f : 1f, vertical ? -1f : 1f);
                canvas.Translate(horizontal ? -bitmap.Width : 0f, vertical ? -bitmap.Height : 0f);
                canvas.DrawBitmap(bitmap, 0, 0);
            }
            return flipped;
        }
    }
}
