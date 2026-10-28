namespace CityLimits.UI;

/// <summary>
/// The budget panel: the year's taxes and upkeep as they stand, last year's, and the tax rate
/// and funding levels, each with a button to lower it and one to raise it. The budget is always
/// automatic; the panel is where the player changes what it works from. Continue or its close
/// button closes it, and the player moves it by its title bar.
/// </summary>
public sealed class BudgetPanel : IMovableWindow
{
    /// <summary>How far one press moves a funding level, in percent.</summary>
    public const int FundingStep = 10;

    private const int FontSize = 14;
    private const int TitleSize = 20;
    private const int LineHeight = 20;
    private const int Padding = 16;
    private const int Width = 460;
    private const int Height = 426;
    private const int ButtonSize = 22;
    private const int ContinueWidth = 110;
    private const int ContinueHeight = 28;

    // The rows of settings, top to bottom; each has a lower and a raise button.
    private enum Setting
    {
        TaxRate,
        Roads,
        Fire,
        Police,
    }

    private static readonly Setting[] Settings = [Setting.TaxRate, Setting.Roads, Setting.Fire, Setting.Police];

    private readonly WindowFrame _frame = new("Budget");

    // The content starts under the title bar, with the funds.
    private const int Top = WindowFrame.TitleHeight + 8;

    /// <summary>What a click on the panel did.</summary>
    public enum Click
    {
        /// <summary>Nothing the panel acts on.</summary>
        None,

        /// <summary>A setting changed.</summary>
        Changed,

        /// <summary>The player asked to close the panel.</summary>
        Close,
    }

    /// <summary>Tells whether a point on the screen is on the panel.</summary>
    public bool Contains(Vector2 point) => Raylib.CheckCollisionPointRec(point, Bounds);

    /// <summary>Tells whether the player is dragging the panel by its title bar.</summary>
    public bool IsDragging => _frame.IsDragging;

    /// <summary>Starts moving the panel when a press lands on its title bar. Tells whether it did.</summary>
    public bool BeginDrag(Vector2 point) => _frame.BeginDrag(point, Bounds);

    /// <summary>Moves the panel with the pointer while the button is held.</summary>
    public void Drag(Vector2 point, bool held) => _frame.Drag(point, held, new Vector2(Width, Height));

    /// <summary>Acts on a click: lowers or raises a setting, or closes the panel.</summary>
    public Click HandleClick(Vector2 point, CityBudget budget)
    {
        ArgumentNullException.ThrowIfNull(budget);

        if (Raylib.CheckCollisionPointRec(point, ContinueBounds()) || WindowFrame.OnClose(point, Bounds))
        {
            return Click.Close;
        }

        for (var row = 0; row < Settings.Length; row++)
        {
            var setting = Settings[row];
            if (Raylib.CheckCollisionPointRec(point, ButtonBounds(row, raise: false)))
            {
                Change(budget, setting, -1);
                return Click.Changed;
            }

            if (Raylib.CheckCollisionPointRec(point, ButtonBounds(row, raise: true)))
            {
                Change(budget, setting, 1);
                return Click.Changed;
            }
        }

        return Click.None;
    }

    /// <summary>
    /// Draws the panel where the player left it, in the middle of the screen at first, over a dimmed map. The reason is why the
    /// panel opened by itself, or null when the player opened it.
    /// </summary>
    public void Draw(CitySimulation city, string? reason)
    {
        ArgumentNullException.ThrowIfNull(city);

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Theme.Dim);
        var bounds = Bounds;
        _frame.Draw(bounds);

        var x = (int)bounds.X + Padding;
        var y = (int)bounds.Y + Top;
        Raylib.DrawText("Funds", x, y + 4, FontSize, Theme.TextDim);
        Raylib.DrawText($"${city.Treasury.Funds}", (int)(bounds.X + bounds.Width) - Padding - Raylib.MeasureText($"${city.Treasury.Funds}", TitleSize), y, TitleSize, city.Treasury.Funds < 0 ? Theme.Error : Theme.Text);
        y += TitleSize + 6;

        if (reason is not null)
        {
            Raylib.DrawText(reason, x, y, FontSize, Theme.Error);
        }

        y += LineHeight + 4;
        var estimate = city.EstimateBudget();
        Raylib.DrawText("This year, as it stands", x, y, FontSize, Theme.TextDim);
        y += LineHeight;

        var budget = city.Budget;
        DrawSetting(0, $"Taxes at {budget.TaxRate}%", $"collects ${estimate.TaxIncome}");
        DrawSetting(1, $"Roads at {budget.RoadFunding}%", $"asks ${estimate.RoadRequest}, pays ${estimate.RoadFunded}");
        DrawSetting(2, $"Fire at {budget.FireFunding}%", $"asks ${estimate.FireRequest}, pays ${estimate.FireFunded}");
        DrawSetting(3, $"Police at {budget.PoliceFunding}%", $"asks ${estimate.PoliceRequest}, pays ${estimate.PoliceFunded}");

        y = RowTop(Settings.Length) + 4;
        Raylib.DrawText($"Ordinances earn ${estimate.OrdinanceIncome}, cost ${estimate.OrdinanceCost}", x, y, FontSize, Theme.Text);
        y += LineHeight;
        Raylib.DrawText($"Cash flow ${estimate.CashFlow}{(estimate.Cut ? ", funding cut: the money runs out" : string.Empty)}", x, y, FontSize, estimate.NeedsAttention ? Theme.Error : Theme.Text);
        y += LineHeight + 8;

        Raylib.DrawText("Last year", x, y, FontSize, Theme.TextDim);
        y += LineHeight;
        var last = budget.LastYear;
        var lastLine = last is null
            ? "The city hasn't settled a year yet"
            : $"Collected ${last.Income}, paid ${last.Expenses}, cash flow ${last.CashFlow}";
        Raylib.DrawText(lastLine, x, y, FontSize, last is { NeedsAttention: true } ? Theme.Error : Theme.Text);

        var go = ContinueBounds();
        Raylib.DrawRectangleRec(go, Theme.PanelBorder);
        Raylib.DrawText("Continue", (int)go.X + ((ContinueWidth - Raylib.MeasureText("Continue", FontSize)) / 2), (int)go.Y + 7, FontSize, Theme.Accent);
    }

    private void DrawSetting(int row, string label, string detail)
    {
        var bounds = Bounds;
        var top = RowTop(row);
        var x = (int)bounds.X + Padding;
        DrawButton(ButtonBounds(row, raise: false), "-");
        DrawButton(ButtonBounds(row, raise: true), "+");
        Raylib.DrawText(label, x + (2 * (ButtonSize + 4)) + 6, top + 4, FontSize, Theme.Text);
        Raylib.DrawText(detail, x + 250, top + 4, FontSize, Theme.TextDim);
    }

    private static void DrawButton(Rectangle bounds, string text)
    {
        Raylib.DrawRectangleRec(bounds, Theme.PanelBorder);
        Raylib.DrawText(text, (int)bounds.X + ((ButtonSize - Raylib.MeasureText(text, FontSize)) / 2), (int)bounds.Y + 4, FontSize, Theme.Accent);
    }

    // The tax rate moves a percent a press; funding moves a tenth. Each stops at its ends.
    private static void Change(CityBudget budget, Setting setting, int direction)
    {
        switch (setting)
        {
            case Setting.TaxRate:
                budget.SetTaxRate(Math.Clamp(budget.TaxRate + direction, 0, CityBudget.MaxTaxRate));
                break;
            case Setting.Roads:
                budget.SetFunding(Step(budget.RoadFunding, direction), budget.FireFunding, budget.PoliceFunding);
                break;
            case Setting.Fire:
                budget.SetFunding(budget.RoadFunding, Step(budget.FireFunding, direction), budget.PoliceFunding);
                break;
            default:
                budget.SetFunding(budget.RoadFunding, budget.FireFunding, Step(budget.PoliceFunding, direction));
                break;
        }
    }

    private static int Step(int level, int direction) => Math.Clamp(level + (direction * FundingStep), 0, CityBudget.FullFunding);

    /// <summary>Where the panel is: in the middle of the screen until the player moves it.</summary>
    public Rectangle Bounds => _frame.Place(
        new Vector2(Width, Height),
        new Vector2((Raylib.GetScreenWidth() - Width) / 2, (Raylib.GetScreenHeight() - Height) / 2));

    // The settings start below the funds, the reason, and the heading for this year.
    private int RowTop(int row) => (int)Bounds.Y + Top + TitleSize + 6 + LineHeight + 4 + LineHeight + (row * (ButtonSize + 8));

    private Rectangle ButtonBounds(int row, bool raise) => new()
    {
        X = Bounds.X + Padding + (raise ? ButtonSize + 4 : 0),
        Y = RowTop(row),
        Width = ButtonSize,
        Height = ButtonSize,
    };

    private Rectangle ContinueBounds() => new()
    {
        X = Bounds.X + Width - Padding - ContinueWidth,
        Y = Bounds.Y + Height - Padding - ContinueHeight,
        Width = ContinueWidth,
        Height = ContinueHeight,
    };
}
