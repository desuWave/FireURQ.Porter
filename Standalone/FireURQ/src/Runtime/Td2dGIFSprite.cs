using System;
using System.IO;
using FireURQ.Runtime;

public sealed class Td2dGIFSprite : Td2dSprite
{
    private const uint TransparencyKey = 0xFFFF00FF;
    private GifFrames? frames;
    private int frame;
    private int speed = 100;
    private bool running = true;
    private double elapsedMilliseconds;
    private byte[]? pendingBitmap;

    private Td2dGIFSprite(string filename)
    {
        base.Create(null!, 0, 0, 10, 10);
        var stream = d2dCoreModule.gD2DE.Resource_CreateStream(filename.Replace('/', '\\'), false);
        if (stream is null)
            return;

        try
        {
            var bytes = new byte[checked((int)(stream.Size - stream.Position))];
            stream.ReadBuffer(bytes, bytes.Length);
            using var data = new MemoryStream(bytes, writable: false);
            frames = new GifFrames(data);

            f_Width = frames.Width;
            f_Height = frames.Height;
            pendingBitmap = frames.EncodeFrameBitmap(0);
        }
        catch
        {
            frames?.Dispose();
            throw;
        }
        finally
        {
            stream.Free();
        }
    }

    public static Td2dGIFSprite Create(string filename) => new(filename);

    public int AniSpeed
    {
        get => frames is { Count: > 1 } ? speed : 0;
        set
        {
            int next = Math.Clamp(value, 0, 1000);
            if (speed != next)
            {
                speed = next;
                elapsedMilliseconds = 0;
            }
        }
    }

    public int Frame
    {
        get => frame;
        set
        {
            if (frames is not { Count: > 0 } decoded)
                return;

            SelectFrame((int)(Math.Abs((long)value) % decoded.Count));
            elapsedMilliseconds = 0;
        }
    }

    public void Start() => running = true;
    public void Stop() => running = false;

    public unsafe void Update(float deltaSeconds)
    {
        if (frames is { Count: > 1 } decoded && running && speed > 0 &&
            float.IsFinite(deltaSeconds) && deltaSeconds > 0)
        {
            elapsedMilliseconds += 1000.0 * deltaSeconds;
            int next = frame;
            int delay = decoded.PlaybackDelayMilliseconds(next, speed);
            if (elapsedMilliseconds >= delay)
            {
                double cycle = 0;
                for (int i = 0; i < decoded.Count; i++)
                    cycle += decoded.PlaybackDelayMilliseconds(i, speed);

                elapsedMilliseconds %= cycle;
                while (elapsedMilliseconds >= delay)
                {
                    elapsedMilliseconds -= delay;
                    next = (next + 1) % decoded.Count;
                    delay = decoded.PlaybackDelayMilliseconds(next, speed);
                }

                if (next != frame)
                    SelectFrame(next);
            }
        }

        if (pendingBitmap is null)
            return;

        fixed (byte* bitmap = pendingBitmap)
        {
            var texture = d2dCoreModule.gD2DE.Texture_CreatePrim(
                (IntPtr)bitmap, (uint)pendingBitmap.Length, false, TransparencyKey);
            if (texture is not null)
            {
                Texture = texture;
                pendingBitmap = null;
            }
        }
    }

    private void SelectFrame(int index)
    {
        pendingBitmap = frames!.EncodeFrameBitmap(index);
        frame = index;
    }

    protected internal override void Cleanup()
    {
        frames?.Dispose();
        frames = null;
        pendingBitmap = null;
        base.Cleanup();
    }
}
