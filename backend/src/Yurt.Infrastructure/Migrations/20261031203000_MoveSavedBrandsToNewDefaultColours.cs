using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Yurt.Infrastructure.Persistence;

#nullable disable

namespace Yurt.Infrastructure.Migrations
{
    /// <summary>
    /// Data only. Brands saved before the new Yurt palette still carry the old default colours (yellow #ffd119 with
    /// orange #ea580c): every new cafe got them written at creation, and saving the Brand screen stores them too. The
    /// customer app shows the saved brand, so those cafes kept the old yellow look. Moves exactly that old default pair
    /// to the new default (carpet red #b83a2b with wood #4a2f1e); any colour a cafe picked itself is left alone.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261031203000_MoveSavedBrandsToNewDefaultColours")]
    public partial class MoveSavedBrandsToNewDefaultColours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "PlatformSettings"
                SET "Value" = regexp_replace(
                        regexp_replace("Value", '"primaryColor"\s*:\s*"#ffd119"', '"primaryColor":"#b83a2b"', 'i'),
                        '"accentColor"\s*:\s*"#ea580c"', '"accentColor":"#4a2f1e"', 'i')
                WHERE "Key" = 'brand.settings'
                  AND "Value" ~* '"primaryColor"\s*:\s*"#ffd119"'
                  AND "Value" ~* '"accentColor"\s*:\s*"#ea580c"';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: the old colours are not brought back.
        }
    }
}
