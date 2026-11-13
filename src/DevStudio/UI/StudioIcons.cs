using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DevStudio.UI
{
    /// <summary>Draws the small studio icons (project tree, toolbar, completion list) at runtime.</summary>
    public static class StudioIcons
    {
        private static readonly Dictionary<string, Bitmap> _cache = new Dictionary<string, Bitmap>(StringComparer.Ordinal);
        private static readonly string[] CompletionKeys =
        {
            "Method", "Property", "Field", "Event", "Class", "Interface",
            "Struct", "Enum", "Keyword", "Namespace", "Identifier"
        };

        public static Bitmap Get(string key)
        {
            lock (_cache)
            {
                Bitmap bitmap;
                if (!_cache.TryGetValue(key, out bitmap))
                {
                    bitmap = Create(key);
                    _cache[key] = bitmap;
                }
                return bitmap;
            }
        }

        public static int CompletionIndex(string kind)
        {
            for (int i = 0; i < CompletionKeys.Length; i++)
            {
                if (string.Equals(CompletionKeys[i], kind, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return CompletionKeys.Length - 1;
        }

        public static ImageList CreateCompletionImageList()
        {
            return CreateImageList(CompletionKeys);
        }

        public static ImageList CreateTreeImageList()
        {
            return CreateImageList("ProjectCs", "ProjectCpp", "FileCs", "FileCpp", "FileC", "FileH",
                "FileCsproj", "FileVcxproj", "FileOther", "References");
        }

        private static ImageList CreateImageList(params string[] keys)
        {
            var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            foreach (var key in keys)
            {
                list.Images.Add(key, Get(key));
            }
            return list;
        }

        private static Bitmap Create(string key)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                switch (key)
                {
                    case "ProjectCs": DrawProjectWindow(g, Color.FromArgb(103, 62, 158)); break;
                    case "ProjectCpp": DrawProjectWindow(g, Color.FromArgb(46, 122, 86)); break;
                    case "FileCs": DrawPage(g, Color.FromArgb(103, 62, 158)); break;
                    case "FileCpp": DrawPage(g, Color.FromArgb(206, 90, 120)); break;
                    case "FileC": DrawPage(g, Color.FromArgb(206, 140, 90)); break;
                    case "FileH": DrawPage(g, Color.FromArgb(170, 120, 200)); break;
                    case "FileCsproj": DrawPage(g, Color.FromArgb(60, 110, 200)); break;
                    case "FileVcxproj": DrawPage(g, Color.FromArgb(40, 140, 90)); break;
                    case "FileOther": DrawPage(g, Color.FromArgb(150, 150, 150)); break;
                    case "References": DrawReferences(g); break;
                    case "Build": DrawHammer(g, false); break;
                    case "Rebuild": DrawHammer(g, true); break;
                    case "Start": DrawStart(g); break;
                    case "Stop": DrawStop(g); break;
                    case "StepOver": DrawStepOver(g); break;
                    case "StepInto": DrawStepInto(g); break;
                    case "StepOut": DrawStepOut(g); break;
                    case "New": DrawNew(g); break;
                    case "Open": DrawOpen(g); break;
                    case "Save": DrawSave(g); break;
                    case "Method": DrawKind(g, Color.FromArgb(136, 93, 187), "M"); break;
                    case "Property": DrawKind(g, Color.FromArgb(66, 122, 228), "P"); break;
                    case "Field": DrawKind(g, Color.FromArgb(107, 165, 227), "F"); break;
                    case "Event": DrawKind(g, Color.FromArgb(224, 99, 66), "E"); break;
                    case "Class": DrawKind(g, Color.FromArgb(26, 158, 142), "C"); break;
                    case "Interface": DrawKind(g, Color.FromArgb(135, 190, 90), "I"); break;
                    case "Struct": DrawKind(g, Color.FromArgb(95, 158, 208), "S"); break;
                    case "Enum": DrawKind(g, Color.FromArgb(229, 150, 50), "N"); break;
                    case "Keyword": DrawKind(g, Color.FromArgb(90, 110, 190), "K"); break;
                    case "Namespace": DrawKind(g, Color.FromArgb(130, 130, 130), "N"); break;
                    default: DrawKind(g, Color.FromArgb(140, 150, 165), "V"); break;
                }
            }
            return bmp;
        }

        private static void DrawProjectWindow(Graphics g, Color title)
        {
            using (var b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, 1, 3, 14, 11);
            }
            using (var b = new SolidBrush(title))
            {
                g.FillRectangle(b, 1, 3, 14, 4);
            }
            using (var p = new Pen(Color.FromArgb(90, 90, 100)))
            {
                g.DrawRectangle(p, 1, 3, 14, 11);
            }
        }

        private static void DrawPage(Graphics g, Color accent)
        {
            using (var b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, 2, 1, 11, 14);
            }
            using (var p = new Pen(Color.FromArgb(120, 120, 130)))
            {
                g.DrawRectangle(p, 2, 1, 11, 14);
            }
            using (var b = new SolidBrush(accent))
            {
                g.FillRectangle(b, 2, 1, 11, 3);
                g.FillRectangle(b, 4, 7, 7, 1);
                g.FillRectangle(b, 4, 10, 7, 1);
                g.FillRectangle(b, 4, 13, 5, 1);
            }
        }

        private static void DrawReferences(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(80, 130, 220)))
            {
                g.FillRectangle(b, 2, 2, 8, 8);
            }
            using (var b = new SolidBrush(Color.FromArgb(150, 190, 240)))
            {
                g.FillRectangle(b, 6, 6, 8, 8);
            }
            using (var p = new Pen(Color.FromArgb(60, 90, 160)))
            {
                g.DrawRectangle(p, 2, 2, 8, 8);
                g.DrawRectangle(p, 6, 6, 8, 8);
            }
        }

        private static void DrawHammer(Graphics g, bool rebuild)
        {
            using (var p = new Pen(Color.FromArgb(110, 80, 50), 2.4f))
            {
                g.DrawLine(p, 6, 7, 13, 14);
            }
            using (var b = new SolidBrush(Color.FromArgb(110, 120, 135)))
            {
                g.FillPolygon(b, new[] { new Point(2, 4), new Point(9, 2), new Point(12, 6), new Point(5, 9) });
            }
            if (rebuild)
            {
                using (var p = new Pen(Color.FromArgb(210, 60, 50), 1.8f))
                {
                    g.DrawArc(p, 7, 1, 7, 7, -60, 240);
                }
                using (var b = new SolidBrush(Color.FromArgb(210, 60, 50)))
                {
                    g.FillPolygon(b, new[] { new Point(15, 3), new Point(11, 1), new Point(14, 6) });
                }
            }
        }

        private static void DrawStart(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(40, 165, 60)))
            {
                g.FillPolygon(b, new[] { new Point(4, 2), new Point(13, 8), new Point(4, 14) });
            }
        }

        private static void DrawStop(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(205, 45, 45)))
            {
                g.FillRectangle(b, 3, 3, 10, 10);
            }
        }

        private static void DrawStepOver(Graphics g)
        {
            using (var p = new Pen(Color.FromArgb(60, 110, 220), 1.8f))
            {
                g.DrawArc(p, 2, 3, 11, 8, 180, 180);
            }
            using (var b = new SolidBrush(Color.FromArgb(60, 110, 220)))
            {
                g.FillPolygon(b, new[] { new Point(12, 8), new Point(8, 5), new Point(8, 11) });
                g.FillEllipse(b, 6, 11, 4, 4);
            }
        }

        private static void DrawStepInto(Graphics g)
        {
            using (var p = new Pen(Color.FromArgb(60, 110, 220), 1.8f))
            {
                g.DrawLine(p, 7, 1, 7, 8);
                g.DrawLine(p, 11, 10, 11, 14);
                g.DrawLine(p, 11, 14, 14, 14);
            }
            using (var b = new SolidBrush(Color.FromArgb(60, 110, 220)))
            {
                g.FillPolygon(b, new[] { new Point(7, 12), new Point(3, 7), new Point(11, 7) });
            }
        }

        private static void DrawStepOut(Graphics g)
        {
            using (var p = new Pen(Color.FromArgb(60, 110, 220), 1.8f))
            {
                g.DrawLine(p, 7, 14, 7, 8);
                g.DrawLine(p, 2, 14, 5, 14);
            }
            using (var b = new SolidBrush(Color.FromArgb(60, 110, 220)))
            {
                g.FillPolygon(b, new[] { new Point(7, 1), new Point(3, 6), new Point(11, 6) });
            }
        }

        private static void DrawNew(Graphics g)
        {
            DrawPage(g, Color.FromArgb(90, 140, 220));
            using (var b = new SolidBrush(Color.FromArgb(210, 120, 30)))
            {
                g.FillRectangle(b, 8, 8, 7, 2);
                g.FillRectangle(b, 10, 6, 2, 7);
            }
        }

        private static void DrawOpen(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(235, 195, 85)))
            {
                g.FillPolygon(b, new[] { new Point(1, 4), new Point(6, 4), new Point(8, 6), new Point(15, 6), new Point(15, 13), new Point(1, 13) });
            }
            using (var p = new Pen(Color.FromArgb(160, 125, 40)))
            {
                g.DrawPolygon(p, new[] { new Point(1, 4), new Point(6, 4), new Point(8, 6), new Point(15, 6), new Point(15, 13), new Point(1, 13) });
            }
        }

        private static void DrawSave(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(60, 100, 190)))
            {
                g.FillRectangle(b, 2, 2, 12, 12);
            }
            using (var b = new SolidBrush(Color.White))
            {
                g.FillRectangle(b, 5, 2, 6, 4);
                g.FillRectangle(b, 4, 9, 8, 5);
            }
            using (var b = new SolidBrush(Color.FromArgb(60, 100, 190)))
            {
                g.FillRectangle(b, 9, 3, 1, 2);
            }
        }

        private static void DrawKind(Graphics g, Color color, string letter)
        {
            using (var b = new SolidBrush(color))
            {
                g.FillRectangle(b, 1, 2, 14, 12);
            }
            using (var p = new Pen(ControlPaint.Dark(color)))
            {
                g.DrawRectangle(p, 1, 2, 14, 12);
            }
            using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(letter, f, b, new RectangleF(0.5f, 1.5f, 15, 13), sf);
            }
        }
    }
}
