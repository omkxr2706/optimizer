using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace HtmlOptimizer
{
    public static class AdvancedOptimizer
    {
        public static string TitleFromFileName(string fileName)
        {
            var name = Path.GetFileName(fileName);

            var idxHtml = name.IndexOf(".html", StringComparison.OrdinalIgnoreCase);
            if (idxHtml >= 0) name = name.Substring(0, idxHtml);

            name = Path.GetFileNameWithoutExtension(name);
            name = Regex.Replace(name, @"[_\-]+", " ").Trim();
            name = Regex.Replace(name, @"\s{2,}", " ").Trim();

            return string.IsNullOrWhiteSpace(name) ? "Consumer Finance Application Form" : name;
        }

        public static string BuildOptimizedHtml(string sourceHtmlOrText, string title, string lang = "en")
        {
            var html = ExtractLikelyHtml(sourceHtmlOrText);

            var cleaned = CleanForPrint(html);
            var withHead = ReplaceHeadWithMinimal(cleaned, title, lang);
            return MinifyAggressive(withHead);
        }

        public static string CleanForPrint(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return "<!DOCTYPE html><html><head></head><body></body></html>";

            var doc = new HtmlDocument
            {
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true,
                OptionWriteEmptyNodes = false
            };
            doc.LoadHtml(html);

            // Remove scripts/external styles/inline style blocks
            RemoveAll(doc, "//script|//link|//style|//noscript");

            // Remove heavy media/embeds (these often inflate PDF output)
            RemoveAll(doc, "//img|//svg|//picture|//source|//video|//audio|//canvas|//iframe|//object|//embed");

            // Remove modals/overlays
            RemoveAll(doc,
                "//*[contains(concat(' ',normalize-space(@class),' '),' modal ') or " +
                "contains(concat(' ',normalize-space(@class),' '),' overlay ') or " +
                "contains(concat(' ',normalize-space(@class),' '),' backdrop ') or " +
                "contains(concat(' ',normalize-space(@class),' '),' popup ')]");

            // Remove inline styles
            foreach (var node in doc.DocumentNode.SelectNodes("//*[@style]") ?? Enumerable.Empty<HtmlNode>())
                node.Attributes.Remove("style");

            // Remove JS event handler attributes (onclick, onload, ...)
            foreach (var el in doc.DocumentNode.SelectNodes("//*") ?? Enumerable.Empty<HtmlNode>())
            {
                foreach (var a in el.Attributes.ToList())
                {
                    if (a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                        el.Attributes.Remove(a);
                }
            }

            // Keep only essential attributes (keep class/id for layout)
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "class","id",
                "colspan","rowspan","scope",
                "type","checked","for","name","value",
                "href","target",
                "role","aria-label","aria-labelledby","aria-describedby",
                "lang","dir"
            };

            foreach (var el in doc.DocumentNode.SelectNodes("//*") ?? Enumerable.Empty<HtmlNode>())
            {
                foreach (var a in el.Attributes.ToList())
                {
                    if (!keep.Contains(a.Name))
                        el.Attributes.Remove(a);
                }
            }

            // Remove comments
            RemoveAll(doc, "//comment()");

            // Remove trivial empty wrappers
            RemoveAll(doc, "//div[not(normalize-space()) and not(*)] | //span[not(normalize-space()) and not(*)]");
            RemoveAll(doc, "//p[not(normalize-space()) and not(*)]");

            NormalizeTextNodes(doc.DocumentNode);

            using var sw = new StringWriter();
            doc.Save(sw);
            return sw.ToString();
        }

        public static string ReplaceHeadWithMinimal(string html, string title, string lang)
        {
            var doc = new HtmlDocument
            {
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true,
                OptionWriteEmptyNodes = false
            };
            doc.LoadHtml(html);

            EnsureHtmlAndBody(doc);

            var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
            if (htmlNode != null)
                htmlNode.SetAttributeValue("lang", string.IsNullOrWhiteSpace(lang) ? "en" : lang);

            var head = doc.DocumentNode.SelectSingleNode("//head");
            if (head == null)
            {
                head = doc.CreateElement("head");
                htmlNode?.PrependChild(head);
            }

            head.RemoveAllChildren();
            head.InnerHtml = BuildMinimalHead(string.IsNullOrWhiteSpace(title) ? "Consumer Finance Application Form" : title);

            using var sw = new StringWriter();
            doc.Save(sw);
            return sw.ToString();
        }

        private static void EnsureHtmlAndBody(HtmlDocument doc)
        {
            var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
            if (htmlNode == null)
            {
                htmlNode = doc.CreateElement("html");
                var existing = doc.DocumentNode.ChildNodes.ToList();
                doc.DocumentNode.RemoveAllChildren();
                doc.DocumentNode.AppendChild(htmlNode);
                foreach (var n in existing) htmlNode.AppendChild(n);
            }

            var head = doc.DocumentNode.SelectSingleNode("//head");
            if (head == null)
            {
                head = doc.CreateElement("head");
                htmlNode.PrependChild(head);
            }

            var body = doc.DocumentNode.SelectSingleNode("//body");
            if (body == null)
            {
                body = doc.CreateElement("body");
                var toMove = htmlNode.ChildNodes
                    .Where(n => !n.Name.Equals("head", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var n in toMove)
                {
                    n.Remove();
                    body.AppendChild(n);
                }

                htmlNode.AppendChild(body);
            }
        }

        private static string BuildMinimalHead(string title)
        {
            var sb = new StringBuilder(16_384);
            sb.Append("<meta charset=\"utf-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.Append("<meta name=\"description\" content=\"Print-optimized form for PDF export.\">");
            sb.Append("<title>").Append(HtmlEntity.Entitize(title)).Append("</title>");
            sb.Append("<style>");
            sb.Append(GetMinimalCss());
            sb.Append("</style>");
            return sb.ToString();
        }

     private static string GetMinimalCss()
{
    return "@page{size:A4;margin:8mm}" +  // A4 via @page [web:3]
           "*{box-sizing:border-box;margin:0;padding:0}" +
           "html,body{background:#fff;color:#000}" +

           // Force a SINGLE font across the document to avoid multiple embedded fonts
           "body{font:10px/1.3 \"Nirmala UI\",system-ui,-apple-system,sans-serif;font-weight:400}" +
           "b,strong,th,h1,h2,h3{font-weight:400}" +   // avoid bold font embedding
           "i,em{font-style:normal}" +                 // avoid italic font embedding

           "a{color:inherit;text-decoration:none}" +
           ".container_cust{width:100%;max-width:190mm;margin:0 auto;padding:0 3mm}" +

           // Headline stays blue, centered (no background graphics needed)
           ".page-title h1{color:#0A58CA;text-align:center;font-size:14px;line-height:1.2;margin:6px 0 8px}" +

           // Boxes + layout alignment
           ".box-wrapper{border:1px solid #000;padding:4px;margin:4px 0;break-inside:avoid-page;page-break-inside:avoid}" +
           ".box-head{font-size:10px;border-bottom:1px solid #000;padding:2px 0;margin:0 0 4px;text-transform:uppercase}" +
           ".box-body{font-size:9px}" +

           ".row{width:100%}" +
           ".row:after{content:'';display:block;clear:both}" +

           ".cust-col-md-6,.cust-col-xs-12,.cust-col-xs-6,.cust-col-xs-4,.cust-col-sm-4,.cust-col-sm-8{padding:2px 3px}" +
           ".cust-col-md-6{float:left;width:50%}" +
           ".cust-col-xs-12{float:left;width:100%}" +
           ".cust-col-xs-6{float:left;width:50%}" +
           ".cust-col-xs-4{float:left;width:33.33%}" +
           ".cust-col-sm-4{float:left;width:33.33%}" +
           ".cust-col-sm-8{float:left;width:66.66%}" +

           ".box{width:100%;overflow:hidden;margin:1px 0}" +
           ".box-label,.box-value{display:inline-block;vertical-align:top;padding:1px 2px}" +
           ".box-label{width:44%;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}" +
           ".box-value{width:54%}" +
           ".box-value:before{content:': '}" +

           "div,p,span,li{white-space:normal}" +

           "table{width:100%;border-collapse:collapse;table-layout:fixed;margin:4px 0;font-size:8.5px}" +
           "th,td{border:1px solid #000;padding:2px 3px;vertical-align:top;overflow-wrap:break-word;word-wrap:break-word}";
}


        public static string MinifyAggressive(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            html = Regex.Replace(html, @">\s+<", "><");
            html = Regex.Replace(html, @"[ \t]{2,}", " ");
            html = Regex.Replace(html, @"\r?\n", "");
            return html.Trim();
        }

        private static void RemoveAll(HtmlDocument doc, string xpath)
        {
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes == null) return;

            foreach (var n in nodes)
                n.Remove(); // HtmlAgilityPack removes node from parent collection [web:47]
        }

        private static void NormalizeTextNodes(HtmlNode root)
        {
            foreach (var t in root.DescendantsAndSelf().Where(n => n.NodeType == HtmlNodeType.Text).ToList())
            {
                var s = HtmlEntity.DeEntitize(t.InnerText ?? string.Empty);
                s = Regex.Replace(s, @"\s+", " ").Trim();

                if (s.Length == 0) t.Remove();
                else t.InnerHtml = HtmlEntity.Entitize(s);
            }
        }

        private static string ExtractLikelyHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "<!DOCTYPE html><html><head></head><body></body></html>";

            var s = input.Trim();

            var i = s.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return s.Substring(i);

            i = s.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return s.Substring(i);

            i = s.IndexOf('<');
            if (i >= 0) return "<!DOCTYPE html><html><head></head><body>" + s.Substring(i) + "</body></html>";

            return "<!DOCTYPE html><html><head></head><body><p>" + HtmlEntity.Entitize(s) + "</p></body></html>";
        }
    }
}
