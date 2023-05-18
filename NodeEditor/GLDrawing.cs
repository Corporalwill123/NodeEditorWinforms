using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Runtime.InteropServices;

using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace NodeEditor
{

    public class GLDrawing
    {
        public static void DrawRectangle(RectangleF rect, Color4 color)
        {
            GL.Begin(PrimitiveType.LineStrip);

            GL.Color4(color);
            GL.Vertex2(rect.Left, rect.Top);
            GL.Vertex2(rect.Right, rect.Top);
            GL.Vertex2(rect.Right, rect.Bottom);
            GL.Vertex2(rect.Left, rect.Bottom);
            GL.Vertex2(rect.Left, rect.Top);

            GL.End();
        }
        public static void FillRectangle(RectangleF rect, Color4 color)
        {
            GL.Begin(PrimitiveType.Quads);

            GL.Color4(color);
            GL.Vertex2(rect.Left, rect.Top);
            GL.Vertex2(rect.Right, rect.Top);
            GL.Vertex2(rect.Right, rect.Bottom);
            GL.Vertex2(rect.Left, rect.Bottom);

            GL.End();
        }
        public static Color4 ToColor4(Color c)
        {
            return new Color4(c.R, c.G, c.B, c.A);
        }
    }

    public class GLGraphics
    {
        public InterpolationMode InterpolationMode { get; set; }
        public SmoothingMode SmoothingMode { get; set; }
        private Dictionary<string, Texture> StringTextures = new Dictionary<string, Texture>();
        private Dictionary<Image, Texture> ImageTextures = new Dictionary<Image, Texture>();

        internal static Graphics TmpGraphics = Graphics.FromImage(new Bitmap(1,1));

        protected class Texture
        {
            public int Width;
            public int Height;
            public float UVWidth;
            public float UVHeight;
            public int Handle;

            public void Draw(float left, float top, float right, float bottom)
            {
                GL.BindTexture(TextureTarget.Texture2D, Handle);
                GL.Enable(EnableCap.Texture2D);

                GL.Color4(Color.White);

                GL.Begin(PrimitiveType.Quads);

                    GL.TexCoord2(0, 0);
                    GL.Vertex2(left, top);

                    GL.TexCoord2(UVWidth, 0);
                    GL.Vertex2(right, top);

                    GL.TexCoord2(0 + UVWidth, UVHeight);
                    GL.Vertex2(right, bottom);

                    GL.TexCoord2(0, UVHeight);
                    GL.Vertex2(left, bottom);

                GL.End();

                GL.Disable(EnableCap.Texture2D);
            }
            public void Draw(RectangleF rect)
            {
                Draw(rect.Left, rect.Top, rect.Left + UVWidth * rect.Width, rect.Top + UVHeight * rect.Height);
            }

            public void Draw(PointF position)
            {
                Draw(position.X, position.Y, position.X + Width, position.Y + Height);
            }
        }

        private static uint NextPowerOfTwo(uint n)
        {
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n++;
            return n;
        }

        private Texture CreateTexture(int width, int height, float resolution, Action<Graphics> fn)
        {
            var bitmap = new Bitmap((int) NextPowerOfTwo((uint) width), (int) NextPowerOfTwo((uint) height));

            using (var gr = Graphics.FromImage(bitmap))
            {
                fn(gr);
            }

            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            var length = bitmapData.Stride * bitmapData.Height;

            byte[] pixels = new byte[length];

            Marshal.Copy(bitmapData.Scan0, pixels, 0, length);
            bitmap.UnlockBits(bitmapData);

            var handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Clamp);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Clamp);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);

            return new Texture
            {
                Width = width,
                Height = height,
                UVWidth = (float) width / bitmap.Width * resolution,
                UVHeight = (float) height / bitmap.Height * resolution,
                Handle = handle,
            };
        }

        private Texture GetStringTexture(string str, Font font, Brush brush)
        {
            Texture tex;
            if (StringTextures.TryGetValue(str, out tex))
                return tex;


            var size = TmpGraphics.MeasureString(str, font);

            tex = CreateTexture(2 * (int) Math.Ceiling(size.Width), 2 * (int) Math.Ceiling(size.Height), 2, (g) =>
            {
                g.ScaleTransform(2, 2);
                g.DrawString(str, font, brush, Point.Empty);
            });

            StringTextures.Add(str, tex);
            return tex;
        }

        private Texture GetImageTexture(Image image)
        {
            Texture tex;
            if (ImageTextures.TryGetValue(image, out tex))
                return tex;

            tex = CreateTexture(image.Width, image.Height, 1, (g) =>
            {
                g.DrawImage(image, 0, 0);
            });

            ImageTextures.Add(image, tex);
            return tex;
        }

        public void DrawRectangle(Pen pen, RectangleF rect)
        {
            GLDrawing.DrawRectangle(rect, GLDrawing.ToColor4(pen.Color));
        }

        public void FillRectangle(Brush brush, RectangleF rect)
        {
            switch (brush)
            {
                case SolidBrush b:
                    GLDrawing.FillRectangle(rect, GLDrawing.ToColor4(b.Color));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        public void DrawString(String str, Font font, Brush brush, PointF position, StringAlignment align = StringAlignment.Near)
        {
            var tex = GetStringTexture(str, font, brush);
            switch (align)
            {
                case StringAlignment.Far:
                    tex.Draw(new PointF(position.X - tex.Width / tex.UVWidth, position.Y));
                    break;
                case StringAlignment.Near:
                    tex.Draw(position);
                    break;
                case StringAlignment.Center:
                    tex.Draw(new PointF(position.X - (tex.Width / tex.UVWidth) / 2, position.Y));
                    break;
            }
        }
        public void DrawImage(Image image, RectangleF rect)
        {
            GetImageTexture(image).Draw(rect);
        }
        public void DrawLines(Pen pen, PointF[] points)
        {
            if (points.Length < 2) return;

            GL.Begin(PrimitiveType.Triangles);

            GL.Color4(pen.Color);
            var width = pen.Width / 2;

            void lineQuad(PointF a, PointF b, out float dx, out float dy, out float d, out float nx, out float ny)
            {
                dx = b.X - a.X;
                dy = b.Y - a.Y;

                d = (float) Math.Sqrt(dx * dx + dy * dy);

                nx = -dy / d * width;
                ny = dx / d * width;
            }

            PointF oa = points[0], ob = points[1];
            float odx, ody, od, onx, ony;

            lineQuad(oa, ob, out odx, out ody, out od, out onx, out ony);

            for (var i = 0; i < points.Length - 1; i++)
            {
                GL.Vertex2(oa.X - onx, oa.Y - ony); // 1
                GL.Vertex2(oa.X + onx, oa.Y + ony); // 2
                GL.Vertex2(ob.X + onx, ob.Y + ony); // 3

                GL.Vertex2(ob.X + onx, ob.Y + ony); // 3
                GL.Vertex2(ob.X - onx, ob.Y - ony); // 4
                GL.Vertex2(oa.X - onx, oa.Y - ony); // 1

                if (i < points.Length - 2)
                {
                    var na = points[i + 1];
                    var nb = points[i + 2];
                    float ndx, ndy, nd, nnx, nny;

                    lineQuad(na, nb, out ndx, out ndy, out nd, out nnx, out nny);

                    GL.Vertex2(na.X, na.Y);
                    GL.Vertex2(na.X - nnx, na.Y - nny);
                    GL.Vertex2(na.X - onx, na.Y - ony);

                    GL.Vertex2(na.X, na.Y);
                    GL.Vertex2(na.X + nnx, na.Y + nny);
                    GL.Vertex2(na.X + onx, na.Y + ony);

                    oa = na;
                    ob = nb;
                    odx = ndx;
                    ody = ndy;
                    od = nd;
                    onx = nnx;
                    ony = nny;
                }
            }

            GL.End();
        }
    }
}
