using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using OnlineLib2Ebook.Configs;
using OnlineLib2Ebook.Extensions;
using OnlineLib2Ebook.Types.Book;
using OnlineLib2Ebook.Types.Readli;

namespace OnlineLib2Ebook.Logic.Getters; 

public class ReadliGetter : GetterBase {
    public ReadliGetter(BookGetterConfig config) : base(config) { }
    protected override Uri SystemUrl => new("https://readli.net");
    public override async Task<Book> Get(Uri url) {
        Init();
        var doc = await _config.Client.GetHtmlDocWithTriesAsync(url);

        // Находимся на странице ридера
        if (GetId(url).StartsWith("chitat-online", StringComparison.InvariantCultureIgnoreCase)) {
            url = GetMainUrl(url, doc); 
            doc = await _config.Client.GetHtmlDocWithTriesAsync(url);
        }

        var pages = long.Parse(doc.GetTextBySelector("span.button-pages__right").Split(' ')[0]);
        var imageDiv = doc.QuerySelector("div.book-image");
        var href = new Uri(url, imageDiv.QuerySelector("a").Attributes["href"].Value);
        var bookId = GetBookId(href);
            
        var name = doc.GetTextBySelector("h1.main-info__title");
        var author = doc.GetTextBySelector("a.main-info__link");
            
        var book = new Book {
            Cover = await GetCover(imageDiv, url),
            Chapters = await FillChapters(bookId, pages, name),
            Title = name,
            Author = author
        };
            
        return book; 
    }

    private static long GetBookId(Uri uri) {
        return long.Parse(uri.Query.Trim('?').Split("&").FirstOrDefault(p => p.StartsWith("b=")).Replace("b=", ""));
    }

    private static Uri GetMainUrl(Uri url, HtmlDocument doc) {
        var href = doc.QuerySelector("h1 a").Attributes["href"].Value;
        return new Uri(url, href);
    }

    private Task<Image> GetCover(HtmlNode doc, Uri bookUri) {
        var imagePath = doc.QuerySelector("img")?.Attributes["src"]?.Value;
        return !string.IsNullOrWhiteSpace(imagePath) ? GetImage(new Uri(bookUri, imagePath)) : Task.FromResult(default(Image));
    }

    private async Task AddChapter(ICollection<Chapter> chapters, Chapter chapter, StringBuilder text) {
        var chapterDoc = text.ToString().HtmlDecode().AsHtmlDoc();
        chapter.Images = await GetImages(chapterDoc, new Uri("https://readli.net/chitat-online/"));
        chapter.Content = chapterDoc.DocumentNode.InnerHtml;
        chapters.Add(chapter);
    }

    private static GenerateMode GetGenerationMode(IList<HtmlNode> nodes) {
        return nodes.First().Name == "h3" ? GenerateMode.ByChapters : GenerateMode.SingleChapter;
    }

    private async Task<List<Chapter>> FillChapters(long bookId, long pages, string name) {
        var chapters = new List<Chapter>();
        Chapter chapter = null;
        var mode = GenerateMode.SingleChapter;
        var text = new StringBuilder();
            
        for (var i = 1; i <= pages; i++) {
            Console.WriteLine($"Получаю страницу {i}/{pages}");
            var page = await _config.Client.GetHtmlDocWithTriesAsync(new Uri($"https://readli.net/chitat-online/?b={bookId}&pg={i}"));
            var content = page.QuerySelector("div.reading__text");
            var nodes = content.QuerySelectorAll("> h3, > p");

            if (i == 1) {
                mode = GetGenerationMode(nodes);
                if (mode == GenerateMode.SingleChapter) {
                    chapter = new Chapter {
                        Title = name
                    };
                }
            }
            
            foreach (var node in nodes) {
                if (mode == GenerateMode.SingleChapter) {
                    text.AppendFormat($"<p>{node.InnerText.HtmlEncode()}</p>");
                } else {
                    if (node.Name == "h3") {
                        if (chapter == null) {
                            chapter = new Chapter {
                                Title = node.InnerText.HtmlDecode()
                            };
                        } else {
                            await AddChapter(chapters, chapter, text);
                            text.Clear();
                            chapter = new Chapter {
                                Title = node.InnerText.HtmlDecode()
                            };
                        }
                    } else {
                        text.AppendFormat($"<p>{node.InnerText.HtmlEncode()}</p>");
                    }
                }
            }
        }
        
        await AddChapter(chapters, chapter, text);
        return chapters;
    }
}