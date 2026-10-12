using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Android.Text;
using Android.Text.Style;
using Android.Graphics;

namespace Romer.CommonLib
{
    /// <summary>
    /// Extremely crude markdown to spannable text converter.
    /// Each block is styled by how it starts: "#" and "##" headings, "-" bullets, and anything else as small plain
    /// text. Nothing else, such as emphasis or links, is handled.
    /// </summary>
    public static class Markdown
    {
        /// <summary>
        /// Convert markdown to styled text.
        /// Blocks are separated by blank lines, and the lines within a block are joined into one paragraph, so each
        /// bullet must be a block of its own.
        /// </summary>
        /// <param name="markdownStm">Stream containing markdown. It is closed on return.</param>
        /// <returns>Styled text</returns>
        public static Java.Lang.ICharSequence ToSpannableString(Stream markdownStm)
        {
            using(var rdr = new StreamReader(markdownStm))
            {
                var blockSb = new StringBuilder();
                var ssb = new SpannableStringBuilder();
                while(!rdr.EndOfStream)
                {
                    blockSb.Clear();

                    // Read a block
                    while(true)
                    {
                        if (rdr.EndOfStream)
                            break;
                        var line = rdr.ReadLine()?.Trim() ?? string.Empty;
                        if (line == string.Empty)
                            break;
                        if (blockSb.Length > 0)
                            blockSb.Append(' ');
                        blockSb.Append(line);
                    }
                    if (blockSb.Length > 0)
                        blockSb.Append(System.Environment.NewLine);

                    // Style the block
                    var blockStr = blockSb.ToString();
                    SpannableString blockSs;
                    if (blockStr.StartsWith("##"))
                    {
                        blockSs = new SpannableString(blockStr.Substring(2).TrimStart());
                        blockSs.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);
                        blockSs.SetSpan(new RelativeSizeSpan(1.05f), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);  // 1.05 = +5%
                    }
                    else if (blockStr.StartsWith("#"))
                    {
                        blockSs = new SpannableString(blockStr.Substring(1).TrimStart());
                        blockSs.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);
                        blockSs.SetSpan(new RelativeSizeSpan(1.1f), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);  // 1.1 = +10%
                    }
                    else if (blockStr.StartsWith("-"))
                    {
                        blockSs = new SpannableString("\u25CF " + blockStr.Substring(1).TrimStart());
                        blockSs.SetSpan(new StyleSpan(TypefaceStyle.Normal), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);
                        blockSs.SetSpan(new RelativeSizeSpan(0.9f), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);  // 0.9 = -10%
                    }
                    else
                    {
                        blockSs = new SpannableString(blockStr);
                        if (blockSs.Length() > 0)
                        {
                            blockSs.SetSpan(new StyleSpan(TypefaceStyle.Normal), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);
                            blockSs.SetSpan(new RelativeSizeSpan(0.8f), 0, blockSs.Length() - 1, SpanTypes.ExclusiveExclusive);  // 0.8 = -20%
                        }
                    }

                    //
                    ssb.Append(blockSs);
                }

                return ssb;
            }
        }
    }
}