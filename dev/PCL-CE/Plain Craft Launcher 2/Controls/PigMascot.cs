using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL;

/// <summary>猪咪主页摆件：原生方块模型，只在可见时播放水平旋转。</summary>
public sealed class PigMascot : UserControl
{
    private readonly Model3DGroup _pig = new();
    private readonly AxisAngleRotation3D _yaw = new(new Vector3D(0, 1, 0), 25);
    private readonly OrthographicCamera _camera = new();
    private readonly List<ConfigItem> _observed = [];
    private readonly ConfigObserver _observer;
    private Window? _window;
    private static readonly Lazy<Material> VanillaSkin = new(CreateVanillaSkin);

    public bool IsPreview { get; set; }

    public PigMascot()
    {
        IsHitTestVisible = false;
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromScRgb(1, 0.4f, 0.4f, 0.4f)));
        scene.Children.Add(new DirectionalLight(Color.FromScRgb(1, 0.6f, 0.6f, 0.6f), new Vector3D(-2, -4, -3)));
        _pig.Transform = new RotateTransform3D(_yaw, new Point3D(0, 7, 3));
        BuildPig();
        scene.Children.Add(_pig);
        var viewport = new Viewport3D { Camera = _camera, ClipToBounds = false };
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        var stage = new Grid { Width = 440, Height = 370 };
        stage.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 180, Height = 22, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 80),
            Fill = new RadialGradientBrush(Color.FromArgb(30, 125, 70, 85), Colors.Transparent)
        });
        stage.Children.Add(viewport);
        Content = new Viewbox { Child = stage, Stretch = Stretch.Uniform, MaxWidth = 520, MaxHeight = 440 };
        _observer = new ConfigObserver(ConfigEvent.Update, _ => Dispatcher.BeginInvoke(() =>
        {
            if (IsLoaded) RefreshSettings();
        }));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdateAnimation();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_observed.Count != 0) return;
        foreach (var key in Config.Preference.Pig.CheckScope(ConfigService.KeySet).Append("UiCustomType"))
        {
            if (!ConfigService.TryGetConfigItemNoType(key, out var item) || item is null) continue;
            item.Observe(_observer);
            _observed.Add(item);
        }
        _window = Window.GetWindow(this);
        if (_window is not null) _window.StateChanged += OnWindowStateChanged;
        RefreshSettings();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        foreach (var item in _observed) item.Unobserve(_observer);
        _observed.Clear();
        if (_window is not null) _window.StateChanged -= OnWindowStateChanged;
        _window = null;
        StopRotation();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e) => UpdateAnimation();

    public void RefreshSettings()
    {
        var settings = Config.Preference.Pig;
        Visibility = IsPreview || (settings.Enabled && Config.Preference.Homepage.Type == 0)
            ? Visibility.Visible : Visibility.Collapsed;
        var tilt = Math.Clamp(settings.Tilt, 0, 35) * Math.PI / 180;
        _camera.Position = new Point3D(0, 7 + 40 * Math.Sin(tilt), 3 + 40 * Math.Cos(tilt));
        _camera.LookDirection = new Vector3D(0, -40 * Math.Sin(tilt), -40 * Math.Cos(tilt));
        _camera.UpDirection = new Vector3D(0, 1, 0);
        _camera.Width = 42 * 100.0 / Math.Clamp(settings.Size, 60, 140);
        UpdateAnimation();
    }

    private void StopRotation()
    {
        var angle = _yaw.Angle % 360;
        _yaw.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
        _yaw.Angle = angle;
    }

    private void UpdateAnimation()
    {
        StopRotation();
        if (!IsLoaded || !IsVisible || _window?.WindowState == WindowState.Minimized) return;
        var settings = Config.Preference.Pig;
        var speed = Math.Clamp(settings.Speed, 0, 120);
        if (!settings.Rotate || speed == 0) return;
        var rotation = new DoubleAnimation(_yaw.Angle, _yaw.Angle + (settings.Reverse ? -360 : 360),
            TimeSpan.FromSeconds(360.0 / speed)) { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(rotation, 60);
        _yaw.BeginAnimation(AxisAngleRotation3D.AngleProperty, rotation);
    }

    private void BuildPig()
    {
        // Java 1.21.1 PigModel / QuadrupedModel 的成年猪静止姿态，单位为模型像素。
        AddMinecraftBox(-4, -4, -8, 8, 8, 8, 0, 0, new Point3D(0, 12, -6));
        AddMinecraftBox(-2, 0, -9, 4, 3, 1, 16, 16, new Point3D(0, 12, -6));
        AddMinecraftBox(-5, -10, -7, 10, 16, 8, 28, 8, new Point3D(0, 11, 2), Math.PI / 2);
        foreach (var x in new[] { -3d, 3d })
        foreach (var z in new[] { 7d, -5d })
            AddMinecraftBox(-2, 0, -2, 4, 6, 4, 0, 16, new Point3D(x, 18, z));
    }

    private static Material CreateVanillaSkin()
    {
        var source = new BitmapImage();
        source.BeginInit();
        source.CacheOption = BitmapCacheOption.OnLoad;
        source.UriSource = new Uri("pack://application:,,,/Plain Craft Launcher 2;component/Images/Pig/vanilla-pig.png");
        source.EndInit();
        source.Freeze();
        // 运行时按最近邻放大，避免 WPF 的 3D 纹理过滤把像素边缘抹糊。
        var original = new byte[64 * 32 * 4];
        new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0).CopyPixels(original, 64 * 4, 0);
        var texels = new byte[1024 * 512 * 4];
        for (var y = 0; y < 512; y++)
        for (var x = 0; x < 1024; x++)
            Buffer.BlockCopy(original, ((y / 16) * 64 + x / 16) * 4, texels, (y * 1024 + x) * 4, 4);
        var texture = BitmapSource.Create(1024, 512, 96, 96, PixelFormats.Bgra32, null, texels, 1024 * 4);
        texture.Freeze();
        var material = new DiffuseMaterial(new ImageBrush(texture)
        {
            Stretch = Stretch.Fill,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1)
        });
        material.Freeze();
        return material;
    }

    private void AddMinecraftBox(double x, double y, double z, double width, double height, double depth,
        int u, int v, Point3D pivot, double rotationX = 0)
    {
        Point3D P(double px, double py, double pz)
        {
            var rotatedY = py * Math.Cos(rotationX) - pz * Math.Sin(rotationX);
            var rotatedZ = py * Math.Sin(rotationX) + pz * Math.Cos(rotationX);
            return new Point3D(pivot.X + px, 24 - pivot.Y - rotatedY, -pivot.Z - rotatedZ);
        }
        Point3D[] corners = [
            P(x,y,z), P(x+width,y,z), P(x+width,y+height,z), P(x,y+height,z),
            P(x,y,z+depth), P(x+width,y,z+depth), P(x+width,y+height,z+depth), P(x,y+height,z+depth)
        ];
        var mesh = new MeshGeometry3D();
        void Face(int a, int b, int c, int d, double u1, double v1, double u2, double v2)
        {
            var start = mesh.Positions.Count;
            foreach (var index in new[] { a, b, c, d }) mesh.Positions.Add(corners[index]);
            foreach (var uv in new[] { new Point(u2, v1), new Point(u1, v1), new Point(u1, v2), new Point(u2, v2) })
                mesh.TextureCoordinates.Add(new Point(uv.X / 64, uv.Y / 32));
            foreach (var index in new[] { 0, 1, 2, 0, 2, 3 }) mesh.TriangleIndices.Add(start + index);
        }
        // ModelPart.Cube 的原版六面 UV 布局，保留顶面、底面及背面的方向。
        Face(5, 4, 0, 1, u+depth, v, u+depth+width, v+depth);
        Face(2, 3, 7, 6, u+depth+width, v+depth, u+depth+width*2, v);
        Face(0, 4, 7, 3, u, v+depth, u+depth, v+depth+height);
        Face(1, 0, 3, 2, u+depth, v+depth, u+depth+width, v+depth+height);
        Face(5, 1, 2, 6, u+depth+width, v+depth, u+depth*2+width, v+depth+height);
        Face(4, 5, 6, 7, u+depth*2+width, v+depth, u+depth*2+width*2, v+depth+height);
        var model = new GeometryModel3D(mesh, VanillaSkin.Value);
        model.Freeze();
        _pig.Children.Add(model);
    }
}
