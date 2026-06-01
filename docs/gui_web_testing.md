# GUI/Web Testing Document

## 1. Overview
This document details the graphical user interface (GUI) and web frontend testing applied to the **Loan Component** for Task 3 (Testing II). To ensure robust, full-stack coverage without needing a physical MySQL server, we utilized an `InMemoryDatabase` combined with two distinct GUI testing approaches:
1. **HTML Parsing & API Emulation (AngleSharp)**
2. **Headless Browser Automation (Microsoft Playwright)**

## 2. Methodology A: HTML Parsing & API Emulation (AngleSharp)

The first suite of tests (`LoanFrontendTests.cs`) utilizes `WebApplicationFactory<Program>` to spin up the ASP.NET Core pipeline in memory. We then use `HttpClient` combined with the **AngleSharp** library to physically parse the returned HTML DOM.

### Advanced Setup Details
Because the application uses ASP.NET Core MVC with `[ValidateAntiForgeryToken]`, standard API requests would fail. This test suite implements an advanced `ExtractAntiForgeryToken` method that:
1. Performs a GET request to the page (e.g., `/Loan/Create`).
2. Uses AngleSharp to parse the `<input name="__RequestVerificationToken">` from the HTML DOM.
3. Injects this token and the associated session cookie into the subsequent POST request to perfectly simulate a legitimate browser form submission.

### Tests Implemented (Full CRUD)

| Test Method | Action | Expected Outcome |
| :--- | :--- | :--- |
| `LoanIndexPage_RendersCorrectly_WithHeaders` | GET `/Loan` | Parses HTML table headers and verifies title. |
| `LoanCreatePage_GET_RendersFormWithDropdowns` | GET `/Loan/Create` | Verifies `<select>` elements load Member/Book data. |
| `LoanCreate_SubmitValidData` | POST `/Loan/Create` | Parses the resulting DOM to find `.alert-success`. |
| `LoanCreate_SubmitWithInvalidDates` | POST `/Loan/Create` | Parses `data-valmsg-for` spans to find validation errors. |
| `LoanCreate_SubmitWithUnavailableBook` | POST `/Loan/Create` | Parses HTML to verify inventory stock boundaries. |
| `LoanEditPage_GET_RendersFormWithPrepopulatedData` | GET `/Loan/Edit/{id}` | Parses `<input>` elements for correct default values. |
| `LoanEdit_SubmitValidData` | POST `/Loan/Edit/{id}` | Verifies HTTP Redirect and success banner in DOM. |
| `LoanEdit_SubmitInvalidDates` | POST `/Loan/Edit/{id}` | Parses DOM to verify the view is re-rendered with errors. |
| `LoanDeletePage_GET_RendersConfirmationDetails` | GET `/Loan/Delete/{id}` | Parses `<dl>` tags to ensure entity details load. |
| `LoanDelete_SubmitConfirmation` | POST `/Loan/Delete/{id}` | Verifies the entity is removed and UI shows success. |

## 3. Methodology B: Headless Browser Automation (Playwright)

While AngleSharp tests the HTML structure and HTTP pipeline, it does not execute JavaScript or physically render the page layout. To guarantee the actual End-User Experience, we implemented **Microsoft Playwright** (`LoanPlaywrightTests.cs`).

### Advanced Setup Details
Playwright requires a real, listening HTTP Server (not just an in-memory pipeline). We created a custom `KestrelWebApplicationFactory` that:
1. Dynamically boots a secondary Kestrel WebHost on a random free TCP port (`127.0.0.1:0`).
2. Attaches the exact same `LibraryDbContext` (InMemory) to both the Test Context and the Kestrel Server.
3. Launches a headless Chromium Browser instance that actually navigates to the URL and interacts with the page.

### Tests Implemented (Full Browser CRUD)

| Test Method | UI Action | Expected Outcome |
| :--- | :--- | :--- |
| `LoanIndex_ViaPlaywrightBrowser` | `page.GotoAsync` | Reads the rendered table directly from the screen. |
| `LoanCreate_ViaPlaywrightBrowser` | `page.SelectOptionAsync`, `page.ClickAsync` | Physically clicks the "Create" button and saves to DB. |
| `LoanCreate_InvalidDates` | `page.FillAsync` | Simulates a user typing a bad date and verifies error text. |
| `LoanEdit_ViaPlaywrightBrowser` | `page.FillAsync`, `page.ClickAsync` | Modifies the `ReturnDate` input box and submits. |
| `LoanDelete_ViaPlaywrightBrowser` | `page.ClickAsync("button.btn-danger")` | Clicks the red delete button and verifies removal. |

## 4. Conclusion
By combining AngleSharp (10 tests) for lightning-fast DOM structure validation, and Playwright (5 tests) for real-world user interaction emulation, the GUI/Web layer of the Loan Component is exhaustively covered. All 15 tests execute flawlessly and completely fulfill the Testing II web automation requirements.
