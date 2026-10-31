using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;

namespace PCL;

public partial class PageSetupPig
{
    private bool _reloading = true;

    public PageSetupPig()
    {
        InitializeComponent();
        SliderSpeed.getHintText = new Func<object, object>(v => $"{v}°/秒");
        SliderSize.getHintText = new Func<object, object>(v => $"{Convert.ToInt32(v) + 60}%");
        SliderTilt.getHintText = new Func<object, object>(v => $"{v}°");
        Loaded += (_, _) => Reload();
    }

    private void Reload()
    {
        _reloading = true;
        try
        {
            var settings = Config.Preference.Pig;
            CheckShow.Checked = settings.Enabled;
            CheckRotate.Checked = settings.Rotate;
            CheckReverse.Checked = settings.Reverse;
            SliderSpeed.Value = Math.Clamp(settings.Speed, 0, 120);
            SliderSize.Value = Math.Clamp(settings.Size, 60, 140) - 60;
            SliderTilt.Value = Math.Clamp(settings.Tilt, 0, 35);
            RefreshLabels();
        }
        finally { _reloading = false; }
    }

    private void RefreshLabels()
    {
        LabSpeed.Text = $"{SliderSpeed.Value}°/秒";
        LabSize.Text = $"{SliderSize.Value + 60}%";
        LabTilt.Text = $"{SliderTilt.Value}°";
    }

    private void CheckChanged(object sender, bool user)
    {
        if (_reloading) return;
        var settings = Config.Preference.Pig;
        if (sender == CheckShow) settings.Enabled = CheckShow.Checked == true;
        else if (sender == CheckRotate) settings.Rotate = CheckRotate.Checked == true;
        else if (sender == CheckReverse) settings.Reverse = CheckReverse.Checked == true;
    }

    private void SliderChanged(object sender, bool user)
    {
        if (_reloading) return;
        var settings = Config.Preference.Pig;
        if (sender == SliderSpeed) settings.Speed = SliderSpeed.Value;
        else if (sender == SliderSize) settings.Size = SliderSize.Value + 60;
        else if (sender == SliderTilt) settings.Tilt = SliderTilt.Value;
        RefreshLabels();
    }

    private void ResetPig(object sender, MouseButtonEventArgs e)
    {
        Config.Preference.Pig.Reset();
        Reload();
    }
}
