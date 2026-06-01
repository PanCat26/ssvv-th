# GUI / Web Testing — Testing II

**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8)
**Scope:** Every web page of all three functionalities — Book/Member/Loan CRUD pages, the Loan workflow pages, and the Report page.

## 1. Approach & tooling

Two complementary layers of GUI/web testing are used, both driving the application through its real HTTP pipeline:

1. **Rendered-HTML / DOM testing** — the app is hosted in-memory with `WebApplicationFactory`; pages are requested over HTTP, parsed with **AngleSharp**, and the DOM (headings, form fields, dropdowns, validation spans, banners, table rows) is asserted. Form posts include the real **anti-forgery token + cookie** scraped from the GET page ([TestHelper](../ssvv-th.Tests/Helpers/TestHelper.cs)), so CSRF-protected flows are exercised end-to-end.
2. **Real-browser automation** — **Microsoft Playwright** launches a headless Chromium against a live Kestrel server, performing real clicks, text entry and `<select>` interactions, then asserting on the rendered DOM after navigation.

To keep the shared in-memory database deterministic, all DOM-level GUI suites run in a single serial xUnit collection ([GuiWebCollection](../ssvv-th.Tests/GuiTests/GuiWebCollection.cs)); each test resets and re-seeds before acting.

| Functionality | Page coverage | DOM suite (AngleSharp) | Browser suite (Playwright) |
| :--- | :--- | :--- | :--- |
| CRUD — Book | Index, Create, Edit, Delete | [BookGuiTests.cs](../ssvv-th.Tests/GuiTests/BookGuiTests.cs) (9) | [CrudReportPlaywrightTests.cs](../ssvv-th.Tests/GuiTests/CrudReportPlaywrightTests.cs) |
| CRUD — Member | Index, Create, Edit, Delete | [MemberGuiTests.cs](../ssvv-th.Tests/GuiTests/MemberGuiTests.cs) (8) | CrudReportPlaywrightTests.cs |
| Loan workflow | Index, Create, Edit, Delete | [LoanFrontendTests.cs](../ssvv-th.Tests/FrontendTests/LoanFrontendTests.cs) (10) | [LoanPlaywrightTests.cs](../ssvv-th.Tests/FrontendTests/LoanPlaywrightTests.cs) (3) |
| Report | Index + filters + CSV | [ReportGuiTests.cs](../ssvv-th.Tests/GuiTests/ReportGuiTests.cs) (5) | CrudReportPlaywrightTests.cs |

---

## 2. Test cases

### 2.1 Book pages (DOM)
| Case | Assertion |
| :--- | :--- |
| Index renders | `<h1>` "Books", "+ Add Book" link to `/Book/Create`, seeded row visible |
| Create GET | inputs for Title, Author, ISBN, AvailableCopies present |
| Create POST valid | 302 redirect; index shows "Book created successfully." and the new title |
| Create POST missing Title | 200; page shows "Title is required." |
| Edit GET | form pre-populated with the book's title |
| Edit POST valid | redirect; index shows "Book updated successfully." |
| Delete GET | confirmation prompt + book details |
| Delete POST | redirect; index shows "Book deleted successfully." |
| Delete referenced by loan | redirect; index shows the "cannot be deleted" error banner |

### 2.2 Member pages (DOM)
| Case | Assertion |
| :--- | :--- |
| Index renders | `<h1>` "Members", add link, seeded e-mail visible |
| Create GET | inputs for Name, Email, Phone present |
| Create POST valid | redirect; "Member created successfully." + new name |
| Create POST invalid e-mail | 200; "Invalid email address." rendered |
| Edit POST valid | redirect; "Member updated successfully." |
| Delete GET | confirmation prompt + member details |
| Delete POST | redirect; "Member deleted successfully." |
| Delete referenced by loan | redirect; "cannot be deleted" error banner |

### 2.3 Loan pages (DOM)
Index headers & create link; Create form with Book/Member dropdowns; invalid-date submission renders the validation span; valid submission redirects with success banner; out-of-stock book submission renders "This book is currently unavailable."; edit pre-population; edit valid/invalid; delete confirmation and success.

### 2.4 Report page (DOM)
| Case | Assertion |
| :--- | :--- |
| Index renders | `<h1>` "Loan Reports", filter inputs (FromDate, ToDate, ReportType `<select>`, SearchTerm), summary cards, both seeded rows |
| Filter = Returned | returned book present, active book absent |
| Search term | matching row present, non-matching row absent |
| Empty data | "No books were loaned for this report." |
| Export CSV | `text/csv` response containing the header row and a book title |

### 2.5 Real-browser (Playwright)
| Case | Interaction |
| :--- | :--- |
| Book create | type into Title/Author/ISBN/Copies, click **Save**, assert success banner |
| Member create | type into Name/Email/Phone, click **Save**, assert success banner |
| Report filter | choose **Returned** in the `<select>`, click **View Report**, assert only returned rows |
| Loan create | choose Book/Member from dropdowns, click **Save**, assert redirect + banner |
| Loan invalid dates | set reversed dates, submit, assert validation message in DOM |
| Loan edit | set ReturnDate, submit, assert success alert |

---

## 3. Result

All **38** GUI/web cases pass (32 DOM-level + 6 real-browser). Together they verify that every page of every functionality renders correctly, that forms validate and submit through the real CSRF-protected pipeline, and that the reporting filters and CSV export behave correctly in a real browser.
