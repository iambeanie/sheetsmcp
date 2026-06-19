using ModelContextProtocol;
using SheetsMcp.Parsing;

namespace SheetsMcp.Tests;

public sealed class ParsingTests
{
    [Fact]
    public void Normalize_accepts_plain_spreadsheet_id()
    {
        var id = "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c";

        Assert.Equal(id, SpreadsheetReference.Normalize(id));
    }

    [Fact]
    public void Normalize_extracts_spreadsheet_id_from_google_url()
    {
        var id = "1MdXEp5BSfUGAAgHD4LPygE9RZXd_nClKsmzBdnaax1c";
        var url = $"https://docs.google.com/spreadsheets/d/{id}/edit#gid=0";

        Assert.Equal(id, SpreadsheetReference.Normalize(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com/spreadsheets/d/abc/edit")]
    [InlineData("not a spreadsheet id")]
    public void Normalize_rejects_invalid_spreadsheet_references(string value)
    {
        Assert.Throws<McpException>(() => SpreadsheetReference.Normalize(value));
    }

    [Fact]
    public void ParseBounded_accepts_quoted_sheet_names()
    {
        var range = A1RangeParser.ParseBounded("'Sales Q1'!A1:C10");

        Assert.Equal("Sales Q1", range.SheetName);
        Assert.Equal("A", range.StartColumn);
        Assert.Equal(1, range.StartRow);
        Assert.Equal("C", range.EndColumn);
        Assert.Equal(10, range.EndRow);
        Assert.Equal(30, range.CellCount);
    }

    [Fact]
    public void ParseBounded_accepts_quoted_sheet_names_with_slashes()
    {
        var range = A1RangeParser.ParseBounded("'FY25/FY26'!A1:B2");

        Assert.Equal("FY25/FY26", range.SheetName);
        Assert.Equal("'FY25/FY26'!A1:B2", range.Original);
        Assert.Equal(4, range.CellCount);
    }

    [Fact]
    public void ParseBounded_normalizes_unquoted_sheet_names_with_slashes()
    {
        var range = A1RangeParser.ParseBounded("FY25/FY26!A1:B2");

        Assert.Equal("FY25/FY26", range.SheetName);
        Assert.Equal("'FY25/FY26'!A1:B2", range.Original);
        Assert.Equal(4, range.CellCount);
    }

    [Theory]
    [InlineData("A:A")]
    [InlineData("1:10")]
    [InlineData("Sheet1!A")]
    [InlineData("Sheet1!A10:A1")]
    [InlineData("'Broken!A1:B2")]
    public void ParseBounded_rejects_unbounded_or_malformed_ranges(string value)
    {
        Assert.Throws<McpException>(() => A1RangeParser.ParseBounded(value));
    }

    [Theory]
    [InlineData("Sheet1", "Sheet1")]
    [InlineData("Sales Q1", "'Sales Q1'")]
    [InlineData("FY25/FY26", "'FY25/FY26'")]
    [InlineData("Owner's Sheet", "'Owner''s Sheet'")]
    public void NormalizeRangeOrSheet_quotes_sheet_names_when_needed(string input, string expected)
    {
        Assert.Equal(expected, A1RangeParser.NormalizeRangeOrSheet(input));
    }

    [Fact]
    public void CellReference_quotes_sheet_names_with_slashes()
    {
        Assert.Equal("'FY25/FY26'!B3", A1RangeParser.CellReference("FY25/FY26", 3, 2));
    }
}
