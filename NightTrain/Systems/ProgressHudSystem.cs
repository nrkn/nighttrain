using System;
using System.Drawing;
using GTA.UI;
// broken - we are going to replace with a system that keeps track of the position on the path for us
public class ProgressHudSystem : ModSubsystemBase
{
    private readonly bool _useShadow;
    private readonly float _fontScale;
    private readonly int _posX;
    private readonly int _posY;
    private readonly PathProgressSystem _progress;


    public ProgressHudSystem(ProgressHudConfig cfg, PathProgressSystem progress)
    {
        _fontScale = cfg.FontScale;
        _posX = cfg.PosX;
        _posY = cfg.PosY;
        _useShadow = cfg.UseShadow;
        _progress = progress;
    }

    public override void Start()
    {
        Notification.PostTicker($"Progress HUD: ~g~{_progress.Length}~s~ points.", true);
    }

    public override void Tick()
    {
        int total = _progress.Length;
        int n = Math.Min(_progress.PreviousIndex + 1, total); // 1-based for display
        float pct = (total > 0) ? (100f * n / total) : 0f;

        DrawText($"{n}/{total}  ({pct:0.0}%)", _posX, _posY, _fontScale, _useShadow);
    }

    private TextElement _el;

    private void DrawText(string text, int x, int y, float scale, bool shadow)
    {
        if (_el == null)
        {
            _el = new TextElement(text, new PointF(x, y), scale, Color.White, GTA.UI.Font.ChaletLondon, Alignment.Left, shadow, true)
            {
                Enabled = true
            };
        }
        else
        {
            _el.Caption = text;
        }

        _el.Draw();
    }
}
