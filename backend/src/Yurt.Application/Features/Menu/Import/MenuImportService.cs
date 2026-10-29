using System.Text;
using Microsoft.EntityFrameworkCore;
using Yurt.Application.Common.Interfaces;
using Yurt.Domain.Entities;

namespace Yurt.Application.Features.Menu.Import;

/// <summary>A problem in the file. Row 0 means the whole file; Code is translated by the admin app.</summary>
public record MenuImportError(int Row, string Code, string? Value = null);

public record MenuImportPreviewItem(int Row, string Category, string Name, decimal Price, List<string> Sizes, bool Exists);

public record MenuImportResult(
    bool Applied,
    int CategoriesNew,
    int CategoriesExisting,
    int ItemsNew,
    int ItemsUpdated,
    int Sizes,
    List<MenuImportError> Errors,
    List<MenuImportPreviewItem> Items);

/// <summary>
/// Fills the menu of the current cafe from a spreadsheet: one row per dish (or per size of a dish). The first row
/// names the columns; Category, Name and Price are required, the rest (Size, Description, the RU/KK translations,
/// Photo link) are optional. A check run reports what would change and every problem by row; the real run saves
/// nothing unless the whole file is clean. Categories and dishes that already exist (same name) are updated, never
/// deleted, so the same file can be uploaded again to change prices.
/// </summary>
public class MenuImportService
{
    public const int MaxRows = 2000;
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private readonly IApplicationDbContext _db;
    private readonly IAuditLogService _audit;
    private readonly IMenuCacheService _menuCache;

    public MenuImportService(IApplicationDbContext db, IAuditLogService audit, IMenuCacheService menuCache)
    {
        _db = db;
        _audit = audit;
        _menuCache = menuCache;
    }

    // ── Columns ──────────────────────────────────────────────────────────────

    private enum Field { Category, Name, Price, Size, Description, Photo }
    private enum Lang { Main, Ru, Kk }

    /// <summary>Header words per field, in English, Russian and Kazakh, compared without case, spaces or punctuation.</summary>
    private static readonly Dictionary<string, Field> FieldWords = new()
    {
        ["category"] = Field.Category, ["категория"] = Field.Category, ["раздел"] = Field.Category, ["санат"] = Field.Category, ["санаты"] = Field.Category, ["бөлім"] = Field.Category,
        ["name"] = Field.Name, ["item"] = Field.Name, ["dish"] = Field.Name, ["название"] = Field.Name, ["наименование"] = Field.Name, ["блюдо"] = Field.Name, ["атауы"] = Field.Name, ["атау"] = Field.Name, ["тағам"] = Field.Name,
        ["price"] = Field.Price, ["цена"] = Field.Price, ["стоимость"] = Field.Price, ["бағасы"] = Field.Price, ["баға"] = Field.Price,
        ["size"] = Field.Size, ["variant"] = Field.Size, ["размер"] = Field.Size, ["объем"] = Field.Size, ["объём"] = Field.Size, ["өлшемі"] = Field.Size, ["өлшем"] = Field.Size, ["көлемі"] = Field.Size,
        ["description"] = Field.Description, ["описание"] = Field.Description, ["состав"] = Field.Description, ["сипаттама"] = Field.Description, ["сипаттамасы"] = Field.Description, ["құрамы"] = Field.Description,
        ["photo"] = Field.Photo, ["photolink"] = Field.Photo, ["image"] = Field.Photo, ["imageurl"] = Field.Photo, ["picture"] = Field.Photo, ["фото"] = Field.Photo, ["ссылканафото"] = Field.Photo, ["картинка"] = Field.Photo, ["сурет"] = Field.Photo, ["суретсілтемесі"] = Field.Photo,
    };

    /// <summary>Endings that mark a translation column, such as "Name RU" or "Название (каз)".</summary>
    private static readonly (string Suffix, Lang Lang)[] LangSuffixes =
    [
        ("russian", Lang.Ru), ("русский", Lang.Ru), ("орысша", Lang.Ru), ("rus", Lang.Ru), ("рус", Lang.Ru), ("ru", Lang.Ru), ("ру", Lang.Ru),
        ("kazakh", Lang.Kk), ("казахский", Lang.Kk), ("қазақша", Lang.Kk), ("каз", Lang.Kk), ("қаз", Lang.Kk), ("kaz", Lang.Kk), ("kk", Lang.Kk), ("kz", Lang.Kk), ("кз", Lang.Kk),
    ];

    private static string Normalize(string header)
    {
        var sb = new StringBuilder();
        foreach (var ch in header.ToLowerInvariant()) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    private static (Field, Lang)? MatchHeader(string header)
    {
        var h = Normalize(header);
        if (h.Length == 0) return null;
        if (FieldWords.TryGetValue(h, out var f)) return (f, Lang.Main);
        foreach (var (suffix, lang) in LangSuffixes)
            if (h.Length > suffix.Length && h.EndsWith(suffix) && FieldWords.TryGetValue(h[..^suffix.Length], out var g))
                return (g, lang);
        return null;
    }

    /// <summary>Column headers of the template, in the language the admin app is shown in.</summary>
    public static IReadOnlyList<string> TemplateHeaders(string lang) => lang switch
    {
        "ru" => ["Категория", "Название", "Цена", "Размер", "Описание", "Ссылка на фото", "Категория KK", "Название KK", "Размер KK", "Описание KK"],
        "kk" => ["Санат", "Атауы", "Бағасы", "Өлшемі", "Сипаттама", "Сурет сілтемесі", "Санат RU", "Атауы RU", "Өлшемі RU", "Сипаттама RU"],
        _ => ["Category", "Name", "Price", "Size", "Description", "Photo link", "Category RU", "Name RU", "Size RU", "Description RU", "Category KK", "Name KK", "Size KK", "Description KK"],
    };

    /// <summary>A ready-to-fill .xlsx: the headers plus a few sample rows the cafe overwrites.</summary>
    public static byte[] Template(string lang)
    {
        var rows = new List<IReadOnlyList<object?>> { TemplateHeaders(lang).Cast<object?>().ToList() };
        switch (lang)
        {
            case "ru":
                rows.Add(["Кофе", "Капучино", 1200m, "250 мл", "Эспрессо и молоко", null, "Кофе", "Капучино", "250 мл", "Эспрессо және сүт"]);
                rows.Add(["Кофе", "Капучино", 1500m, "350 мл", "Эспрессо и молоко", null, "Кофе", "Капучино", "350 мл", "Эспрессо және сүт"]);
                rows.Add(["Выпечка", "Круассан", 900m, null, "Сливочный", null, "Тоқаштар", "Круассан", null, "Сарымайлы"]);
                break;
            case "kk":
                rows.Add(["Кофе", "Капучино", 1200m, "250 мл", "Эспрессо және сүт", null, "Кофе", "Капучино", "250 мл", "Эспрессо и молоко"]);
                rows.Add(["Кофе", "Капучино", 1500m, "350 мл", "Эспрессо және сүт", null, "Кофе", "Капучино", "350 мл", "Эспрессо и молоко"]);
                rows.Add(["Тоқаштар", "Круассан", 900m, null, "Сарымайлы", null, "Выпечка", "Круассан", null, "Сливочный"]);
                break;
            default:
                rows.Add(["Coffee", "Cappuccino", 1200m, "250 ml", "Espresso and milk", null, "Кофе", "Капучино", "250 мл", "Эспрессо и молоко", "Кофе", "Капучино", "250 мл", "Эспрессо және сүт"]);
                rows.Add(["Coffee", "Cappuccino", 1500m, "350 ml", "Espresso and milk", null, "Кофе", "Капучино", "350 мл", "Эспрессо и молоко", "Кофе", "Капучино", "350 мл", "Эспрессо және сүт"]);
                rows.Add(["Bakery", "Croissant", 900m, null, "Butter croissant", null, "Выпечка", "Круассан", null, "Сливочный", "Тоқаштар", "Круассан", null, "Сарымайлы"]);
                break;
        }
        return SpreadsheetWriter.Write(lang == "ru" ? "Меню" : lang == "kk" ? "Мәзір" : "Menu", rows);
    }

    // ── Parsing ──────────────────────────────────────────────────────────────

    private sealed class Texts
    {
        public string? Main, Ru, Kk;
        /// <summary>The name shown when there is no translation: the main column, else the Russian, else the Kazakh one.</summary>
        public string? Base => Main ?? Ru ?? Kk;
    }

    private sealed class Row
    {
        public int Number;
        public Texts Category = new(), Name = new(), Size = new(), Description = new();
        public decimal Price;
        public string? Photo;
    }

    private sealed class Dish
    {
        public required Row First;
        public List<Row> Rows = [];
        public bool HasSizes => First.Size.Base is not null;
    }

    private static List<Row> ParseRows(List<string[]> sheet, List<MenuImportError> errors)
    {
        var rows = new List<Row>();
        var headerIndex = sheet.FindIndex(r => r.Any(c => !string.IsNullOrWhiteSpace(c)));
        if (headerIndex < 0)
        {
            errors.Add(new(0, "empty"));
            return rows;
        }

        var columns = new Dictionary<(Field, Lang), int>();
        var header = sheet[headerIndex];
        for (var i = 0; i < header.Length; i++)
            if (MatchHeader(header[i]) is { } key) columns.TryAdd(key, i);

        // The name and category may come only in a translation column (a menu written in Russian only).
        bool HasAny(Field f) => columns.Keys.Any(k => k.Item1 == f);
        var missing = new[] { Field.Category, Field.Name, Field.Price }.Where(f => !HasAny(f)).Select(f => f.ToString().ToLowerInvariant()).ToList();
        if (missing.Count > 0)
        {
            errors.Add(new(0, "missingColumns", string.Join(",", missing)));
            return rows;
        }

        var dataRows = sheet.Count - headerIndex - 1;
        if (dataRows > MaxRows)
        {
            errors.Add(new(0, "tooManyRows", MaxRows.ToString()));
            return rows;
        }

        for (var r = headerIndex + 1; r < sheet.Count; r++)
        {
            var cells = sheet[r];
            if (cells.All(string.IsNullOrWhiteSpace)) continue;
            var number = r + 1;
            string? Cell(Field f, Lang l) =>
                columns.TryGetValue((f, l), out var i) && i < cells.Length && !string.IsNullOrWhiteSpace(cells[i])
                    ? string.Join(' ', cells[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                    : null;
            Texts TextsOf(Field f) => new() { Main = Cell(f, Lang.Main), Ru = Cell(f, Lang.Ru), Kk = Cell(f, Lang.Kk) };

            var row = new Row
            {
                Number = number,
                Category = TextsOf(Field.Category),
                Name = TextsOf(Field.Name),
                Size = TextsOf(Field.Size),
                Description = TextsOf(Field.Description),
                Photo = Cell(Field.Photo, Lang.Main),
            };

            var ok = true;
            if (row.Category.Base is null) { errors.Add(new(number, "noCategory")); ok = false; }
            if (row.Name.Base is null) { errors.Add(new(number, "noName")); ok = false; }

            var priceText = Cell(Field.Price, Lang.Main) ?? Cell(Field.Price, Lang.Ru) ?? Cell(Field.Price, Lang.Kk);
            var price = SpreadsheetReader.ParsePrice(priceText);
            if (price is null || price < 0 || price > 10_000_000)
            {
                errors.Add(new(number, priceText is null ? "noPrice" : "badPrice", priceText));
                ok = false;
            }
            else row.Price = Math.Round(price.Value, 2);

            if (row.Photo is not null && !(Uri.TryCreate(row.Photo, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps))
            {
                errors.Add(new(number, "badPhoto", row.Photo));
                ok = false;
            }

            foreach (var (field, text, max) in new[]
            {
                ("category", row.Category, 100), ("name", row.Name, 200), ("size", row.Size, 100), ("description", row.Description, 1000),
            })
            {
                if (new[] { text.Main, text.Ru, text.Kk }.Any(s => s?.Length > max))
                {
                    errors.Add(new(number, "tooLong", field));
                    ok = false;
                }
            }
            if (row.Photo?.Length > 500) { errors.Add(new(number, "tooLong", "photo")); ok = false; }

            if (ok) rows.Add(row);
        }

        if (rows.Count == 0 && errors.Count == 0) errors.Add(new(0, "empty"));
        return rows;
    }

    private static string Key(string? s) => (s ?? "").Trim().ToLowerInvariant();

    /// <summary>Rows of the same dish (same category and name) become one dish; each of its rows is then one size.</summary>
    private static List<Dish> GroupDishes(List<Row> rows, List<MenuImportError> errors)
    {
        var dishes = new List<Dish>();
        var byKey = new Dictionary<(string, string), Dish>();
        foreach (var row in rows)
        {
            var key = (Key(row.Category.Base), Key(row.Name.Base));
            if (!byKey.TryGetValue(key, out var dish))
            {
                dish = new Dish { First = row };
                byKey[key] = dish;
                dishes.Add(dish);
            }
            else if (dish.HasSizes != (row.Size.Base is not null))
            {
                errors.Add(new(row.Number, "mixedSizes", row.Name.Base));
                continue;
            }
            else if (!dish.HasSizes)
            {
                errors.Add(new(row.Number, "duplicate", row.Name.Base));
                continue;
            }
            else if (dish.Rows.Any(x => Key(x.Size.Base) == Key(row.Size.Base)))
            {
                errors.Add(new(row.Number, "duplicateSize", $"{row.Name.Base} — {row.Size.Base}"));
                continue;
            }
            dish.Rows.Add(row);
        }
        return dishes;
    }

    // ── Check and save ───────────────────────────────────────────────────────

    /// <summary>Reads the file and, when <paramref name="apply"/> is set and the file has no problems, saves it.</summary>
    public async Task<MenuImportResult> ImportAsync(Stream file, string fileName, bool apply, CancellationToken ct = default)
    {
        var errors = new List<MenuImportError>();
        List<string[]> sheet;
        try
        {
            sheet = SpreadsheetReader.Read(file, fileName);
        }
        catch (SpreadsheetException)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            errors.Add(new(0, ext == ".xls" ? "oldXls" : ext is ".xlsx" or ".csv" or ".txt" ? "badFile" : "wrongType"));
            return Empty(errors);
        }
        catch (System.Xml.XmlException)
        {
            errors.Add(new(0, "badFile"));
            return Empty(errors);
        }

        var rows = ParseRows(sheet, errors);
        var dishes = GroupDishes(rows, errors);
        errors.Sort((a, b) => a.Row.CompareTo(b.Row));

        var categories = await _db.MenuCategories.ToListAsync(ct);
        var items = await _db.MenuItems.Include(i => i.Variants).ToListAsync(ct);

        // Match file categories to existing ones by any of their names.
        MenuCategory? FindCategory(Texts t) => categories.FirstOrDefault(c =>
            Key(c.Name) == Key(t.Base) || (t.Ru is not null && Key(c.NameRu) == Key(t.Ru)) || (t.Kk is not null && Key(c.NameKk) == Key(t.Kk)));
        MenuItem? FindItem(MenuCategory? cat, Texts t) => cat is null ? null : items.FirstOrDefault(i =>
            i.CategoryId == cat.Id && (Key(i.Name) == Key(t.Base) || (t.Ru is not null && Key(i.NameRu) == Key(t.Ru)) || (t.Kk is not null && Key(i.NameKk) == Key(t.Kk))));

        var fileCategories = dishes.Select(d => d.First.Category).GroupBy(t => Key(t.Base)).Select(g => g.First()).ToList();
        var categoriesExisting = fileCategories.Count(t => FindCategory(t) is not null);
        var preview = dishes.Select(d =>
        {
            var exists = FindItem(FindCategory(d.First.Category), d.First.Name) is not null;
            return new MenuImportPreviewItem(d.First.Number, d.First.Category.Base!, d.First.Name.Base!, d.First.Price,
                d.HasSizes ? d.Rows.Select(r => r.Size.Base!).ToList() : [], exists);
        }).ToList();
        var itemsExisting = preview.Count(p => p.Exists);
        var sizes = dishes.Where(d => d.HasSizes).Sum(d => d.Rows.Count);

        MenuImportResult Summary(bool applied) => new(applied, fileCategories.Count - categoriesExisting, categoriesExisting,
            dishes.Count - itemsExisting, itemsExisting, sizes, errors, preview);

        if (!apply || errors.Count > 0 || dishes.Count == 0) return Summary(false);

        var nextCategorySort = categories.Count == 0 ? 0 : categories.Max(c => c.SortOrder) + 1;
        foreach (var dish in dishes)
        {
            var c = dish.First.Category;
            var cat = FindCategory(c);
            if (cat is null)
            {
                cat = new MenuCategory { Name = c.Base!, NameRu = c.Ru, NameKk = c.Kk, SortOrder = nextCategorySort++ };
                _db.MenuCategories.Add(cat);
                categories.Add(cat);
            }
            else
            {
                if (c.Ru is not null) cat.NameRu = c.Ru;
                if (c.Kk is not null) cat.NameKk = c.Kk;
            }

            var first = dish.First;
            var item = FindItem(cat, first.Name);
            if (item is null)
            {
                item = new MenuItem
                {
                    CategoryId = cat.Id,
                    Name = first.Name.Base!,
                    SortOrder = items.Count(i => i.CategoryId == cat.Id),
                    IsAvailable = true,
                };
                _db.MenuItems.Add(item);
                items.Add(item);
            }
            else item.UpdatedAt = DateTime.UtcNow;

            item.Price = first.Price;
            if (first.Name.Ru is not null) item.NameRu = first.Name.Ru;
            if (first.Name.Kk is not null) item.NameKk = first.Name.Kk;
            if (first.Description.Main is not null) item.Description = first.Description.Main;
            else if (first.Description.Base is not null && string.IsNullOrEmpty(item.Description)) item.Description = first.Description.Base;
            if (first.Description.Ru is not null) item.DescriptionRu = first.Description.Ru;
            if (first.Description.Kk is not null) item.DescriptionKk = first.Description.Kk;
            if (first.Photo is not null) item.ImageUrl = first.Photo;

            if (!dish.HasSizes) continue;
            var nextVariantSort = item.Variants.Count == 0 ? 0 : item.Variants.Max(v => v.SortOrder) + 1;
            foreach (var row in dish.Rows)
            {
                var s = row.Size;
                var variant = item.Variants.FirstOrDefault(v =>
                    Key(v.Label) == Key(s.Base) || (s.Ru is not null && Key(v.LabelRu) == Key(s.Ru)) || (s.Kk is not null && Key(v.LabelKk) == Key(s.Kk)));
                if (variant is null)
                {
                    variant = new MenuItemVariant
                    {
                        MenuItemId = item.Id,
                        Label = s.Base!,
                        SortOrder = nextVariantSort++,
                        IsDefault = item.Variants.Count == 0,
                    };
                    _db.MenuItemVariants.Add(variant);
                    item.Variants.Add(variant);
                }
                variant.Price = row.Price;
                if (s.Ru is not null) variant.LabelRu = s.Ru;
                if (s.Kk is not null) variant.LabelKk = s.Kk;
            }
        }

        await _db.SaveChangesAsync(ct);
        var summary = Summary(true);
        await _audit.LogAsync("MenuImported", "MenuItem", null,
            $"{fileName}: {summary.ItemsNew} new, {summary.ItemsUpdated} updated, {summary.CategoriesNew} new categories", ct);
        await _menuCache.InvalidateAllMenuAsync();
        return summary;
    }

    private static MenuImportResult Empty(List<MenuImportError> errors) => new(false, 0, 0, 0, 0, 0, errors, []);
}
