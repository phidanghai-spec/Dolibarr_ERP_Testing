using DolibarrTests.Helpers;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace DolibarrTests.Pages;

/// <summary>
/// Page Object cho trang danh sach khach hang (Third parties).
/// URL: {BaseUrl}/societe/list.php
///
/// Tim kiem: dien o "Search name" (input name="search_nom"), nhan Enter hoac click nut Search.
/// Ket qua: cac dong trong table.tagtable tbody tr.
/// Dolibarr 22: input id="search_nom" name="search_nom" trong form search.
/// </summary>
public class CustomerListPage
{
    private readonly IWebDriver _driver;

    // -- Locators --
    private static readonly By SearchNameInput = By.Name("search_nom");
    private static readonly By SearchButton = By.CssSelector("button[name^='button_search'], button.button_search, input[name^='button_search']");
    private static readonly By ResultRows = By.CssSelector("table.tagtable tbody tr.oddeven, table.tagtable tr[data-rowid]");
    private static readonly By NoResultCell = By.CssSelector("table.tagtable span.opacitymedium, table.tagtable td.opacitymedium, table.tagtable td[colspan]");

    public CustomerListPage(IWebDriver driver) => _driver = driver;

    /// <summary>Dieu huong den trang danh sach khach hang.</summary>
    public void GoTo()
    {
        _driver.Navigate().GoToUrl($"{TestConfig.BaseUrl}/societe/list.php?type=c");
        WaitHelper.WaitVisible(_driver, By.Name("search_nom"), timeoutSeconds: 15);
    }

    /// <summary>
    /// Tim kiem khach hang theo ten.
    /// Xoa ket qua cu -> nhap ten moi -> nhan nut Search.
    /// </summary>
    public void SearchByName(string name)
    {
        var input = WaitHelper.WaitVisible(_driver, SearchNameInput, timeoutSeconds: 10);
        input.Clear();
        input.SendKeys(name);

        // Click nut Search (uu tien) hoac nhan Enter
        try
        {
            var btn = WaitHelper.WaitClickable(_driver, SearchButton, timeoutSeconds: 5);
            btn.Click();
        }
        catch (WebDriverTimeoutException)
        {
            input.SendKeys(Keys.Enter);
        }

        // Cho trang tai lai (URL co the chua search_nom=...)
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(TestConfig.TimeoutSeconds));
        try
        {
            wait.Until(d => d.Url.Contains("search_nom", StringComparison.OrdinalIgnoreCase)
                         || d.Url.Contains("societe/list", StringComparison.OrdinalIgnoreCase));
        }
        catch (WebDriverTimeoutException) { }
    }

    /// <summary>
    /// Lay so luong dong ket qua trong bang (khong tinh header va footer).
    /// Tra ve 0 neu khong co ket qua hoac co thong bao "Khong co ban ghi".
    /// </summary>
    public int GetResultCount()
    {
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(TestConfig.TimeoutSeconds));
        try
        {
            wait.Until(d =>
            {
                try { d.FindElement(ResultRows); return true; }
                catch (NoSuchElementException) { }
                try
                {
                    var cells = d.FindElements(NoResultCell);
                    foreach (var c in cells)
                    {
                        if (c.Text.Length > 0) return true;
                    }
                }
                catch { }
                return false;
            });
        }
        catch (WebDriverTimeoutException) { }

        try
        {
            var rows = _driver.FindElements(ResultRows);
            // Loc bo nhung dong rong hoac chi la spacer
            int count = 0;
            foreach (var row in rows)
            {
                string text = row.Text.Trim();
                if (!string.IsNullOrEmpty(text)) count++;
            }
            return count;
        }
        catch (NoSuchElementException) { return 0; }
    }

    /// <summary>
    /// Lay danh sach ten khach hang tu ket qua tim kiem (cot ten trong bang).
    /// Dolibarr 22: cot ten o td ke sau td thu nhat (STT), hoac dung class/data attribute.
    /// Dung link text trong cot ten de lay gia tri.
    /// </summary>
    public List<string> GetResultNames()
    {
        var names = new List<string>();
        try
        {
            var rows = _driver.FindElements(ResultRows);
            foreach (var row in rows)
            {
                // Tim td chua link den trang chi tiet KH (societe/card.php hoac /societe/card.php)
                try
                {
                    var links = row.FindElements(By.CssSelector("td a[href*='societe/card.php']"));
                    if (links.Count > 0)
                    {
                        string name = links[0].Text.Trim();
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                }
                catch (NoSuchElementException) { }
                catch (StaleElementReferenceException) { }
            }
        }
        catch (NoSuchElementException) { }
        return names;
    }

    /// <summary>
    /// Kiem tra co thong bao "Khong co ban ghi" (No records) hay khong.
    /// Dolibarr hien "Aucun enregistrement" hoac text tu dich trong td.opacitymedium.
    /// </summary>
    public bool HasNoResultMessage()
    {
        try
        {
            var cells = _driver.FindElements(NoResultCell);
            foreach (var cell in cells)
            {
                string text = cell.Text.Trim().ToLowerInvariant();
                if (text.Contains("no record") || text.Contains("aucun") ||
                    text.Contains("khong") || text.Contains("0 record"))
                    return true;
            }
        }
        catch (NoSuchElementException) { }
        return GetResultCount() == 0;
    }

    /// <summary>
    /// Lay URL hien tai (de debug).
    /// </summary>
    public string GetCurrentUrl() => _driver.Url;
}

