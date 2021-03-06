﻿using System;
using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;
using RT.Util.Lingo;

namespace TankIconMaker.Effects
{
    class WaveEffect : EffectBase
    {
        public override int Version { get { return 1; } }
        public override string TypeName { get { return App.Translation.EffectWave.EffectName; } }
        public override string TypeDescription { get { return App.Translation.EffectWave.EffectDescription; } }

        public double Amplitude { get { return _Amplitude; } set { _Amplitude = Math.Max(0.0, value); } }
        private double _Amplitude;
        public static MemberTr AmplitudeTr(Translation tr) { return new MemberTr(tr.Category.Settings, tr.EffectWave.Amplitude); }

        public double Length { get { return _Length; } set { _Length = Math.Max(0.001, value); } }
        private double _Length;
        public static MemberTr LengthTr(Translation tr) { return new MemberTr(tr.Category.Settings, tr.EffectWave.Length); }

        public WaveEffect()
        {
            Amplitude = 0;
            Length = 1;
        }

        public override BitmapBase Apply(RenderTask renderTask, BitmapBase layer)
        {
            Tank tank = renderTask.Tank;
            if (Amplitude == 0)
                return layer;

            using (var image = layer.ToMagickImage())
            {
                image.BackgroundColor = MagickColors.Transparent;
                image.Wave(PixelInterpolateMethod.Bilinear, Amplitude, Length);

                layer.CopyPixelsFrom(image.ToBitmapSource());
                return layer;
            }
        }
    }
}
