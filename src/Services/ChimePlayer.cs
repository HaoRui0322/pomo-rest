using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace PomoRest.Services
{
    public sealed class ChimePlayer
    {
        private readonly byte[] _wav;

        public ChimePlayer()
        {
            _wav = BuildWav();
        }

        public void Play()
        {
            if (_wav == null || _wav.Length == 0) return;
            byte[] copy = _wav;
            Task.Factory.StartNew(delegate()
            {
                try
                {
                    using (var ms = new MemoryStream(copy, false))
                    {
                        var player = new SoundPlayer(ms);
                        player.PlaySync();
                    }
                }
                catch
                {
                    // Sound is best-effort.
                }
            });
        }

        private static byte[] BuildWav()
        {
            try
            {
                const int sampleRate = 44100;
                const double duration = 0.6;
                int samples = (int)(sampleRate * duration);
                var pcm = new short[samples];

                double[] notes = new double[] { 880.0, 1174.66 };
                double noteLen = duration / (notes.Length + 0.4);

                for (int i = 0; i < samples; i++)
                {
                    double t = (double)i / sampleRate;
                    double v = 0.0;
                    for (int n = 0; n < notes.Length; n++)
                    {
                        double start = n * noteLen * 0.9;
                        double local = t - start;
                        if (local >= 0 && local < noteLen)
                        {
                            double env = Math.Sin(Math.PI * local / noteLen);
                            v += env * Math.Sin(2.0 * Math.PI * notes[n] * local);
                        }
                    }
                    // gentle attack/decay over the whole note
                    double master = Math.Min(1.0, t / 0.01) * Math.Min(1.0, (duration - t) / 0.08);
                    v *= 0.32 * master;
                    pcm[i] = (short)(Math.Max(-1.0, Math.Min(1.0, v)) * 32767);
                }

                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    int dataLen = pcm.Length * 2;
                    bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                    bw.Write(36 + dataLen);
                    bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                    bw.Write(new char[] { 'f', 'm', 't', ' ' });
                    bw.Write(16);
                    bw.Write((short)1);
                    bw.Write((short)1);
                    bw.Write(sampleRate);
                    bw.Write(sampleRate * 2);
                    bw.Write((short)2);
                    bw.Write((short)16);
                    bw.Write(new char[] { 'd', 'a', 't', 'a' });
                    bw.Write(dataLen);
                    foreach (short s in pcm) bw.Write(s);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
