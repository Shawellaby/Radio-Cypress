namespace Shawellaby.RadioCypress.Services.Audio;

public sealed class AudioSampleEventArgs : EventArgs
{
    public AudioSampleEventArgs(float left, float right)
    {
        Left = left;
        Right = right;
    }

    public float Left { get; }

    public float Right { get; }
}