# Integration Testing — Testing I

**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8)
**Goal:** Verify that the layers (**Controller → Service → `LibraryDbContext` → EF Core provider**) work together correctly, including transactions, change tracking, relational reads, and cross-entity business rules.
**Scope:** CRUD across all three entities, the Loan borrow/return workflow, and the Report.

## 1. Strategy

- **Approach:** Bottom-up / component integration. Each test instantiates the **real** service and controller objects wired to a **real** `LibraryDbContext`.
- **Test double:** the only substitution is the database provider — the production MySQL provider is replaced by EF Core's **In-Memory provider** so tests are deterministic and dependency-free while still exercising EF Core's mapping, change-tracking and `SaveChanges` pipeline.
- **Isolation:** every test gets a fresh database keyed by a `Guid` ([InMemoryDb.Create()](../ssvv-th.Tests/Helpers/InMemoryDb.cs)), so tests never interfere with each other and can run in parallel.
- **Controller integration** uses an in-memory `TempData` provider ([ControllerTestHelper](../ssvv-th.Tests/Helpers/ControllerTestHelper.cs)) so the controllers' redirect-with-banner behaviour can be driven without the full HTTP stack.

| Functionality | Integrated components | Test file | Cases |
| :--- | :--- | :--- | :--- |
| CRUD (Book, Member) | `BookController`/`MemberController` → service → DbContext | [CrudIntegrationTests.cs](../ssvv-th.Tests/IntegrationTests/CrudIntegrationTests.cs) | 8 |
| Loan workflow | `LoanService` → DbContext (+ Book/Member) | [LoanWorkflowIntegrationTests.cs](../ssvv-th.Tests/IntegrationTests/LoanWorkflowIntegrationTests.cs) | 4 |
| Loan controller | `LoanController` → service → DbContext | [LoanControllerIntegrationTests.cs](../ssvv-th.Tests/IntegrationTests/LoanControllerIntegrationTests.cs) | 7 |
| Report | `ReportController` → `ReportService` → DbContext | [ReportIntegrationTests.cs](../ssvv-th.Tests/IntegrationTests/ReportIntegrationTests.cs) | 3 |

---

## 2. CRUD integration

Each entity is driven through its controller and the result is verified by reading the persisted row back from the database.

| Test | Verifies |
| :--- | :--- |
| `Book_Create_PersistsThroughAllLayers` | controller → service → DB insert; redirect to Index |
| `Book_Edit_UpdatesPersistedRow` | update flows to DB; every field changed |
| `Book_Delete_RemovesPersistedRow` | delete flows to DB |
| `Book_Delete_WhenReferencedByLoan_IsBlockedWithError` | `InvalidOperationException` from the service is caught by the controller, surfaced as `TempData["Error"]`, and the row is **kept** |
| `Member_Create_PersistsThroughAllLayers` | controller → service → DB insert |
| `Member_Edit_UpdatesPersistedRow` | update flows to DB |
| `Member_Delete_RemovesPersistedRow` | delete flows to DB |
| `Member_Delete_WhenReferencedByLoan_IsBlockedWithError` | referential-integrity guard surfaced gracefully |

---

## 3. Loan workflow integration (cross-entity)

The loan workflow is the functionality that touches **all three entities** (a loan links a Member and decrements a Book's stock). These tests walk multiple operations against a single database to prove state is maintained across calls.

| Test | Scenario | Key assertions |
| :--- | :--- | :--- |
| `FullLifecycle_BorrowReturnDelete_KeepsInventoryConsistent` | Create (active) → Update (return) → Delete | copies 2 → 1 → 2 → 2; loan removed |
| `DeletingActiveLoan_RestoresInventory` | Create (active) → Delete while active | copies 1 → 0 → 1 |
| `CreatedLoan_ExposesBookAndMemberNavigation` | Create then re-read with `.Include` | `Book` and `Member` navigation populated |
| `LoanController_Create_DecrementsInventoryAndRedirects` | full controller path | copies decremented, loan persisted, redirect to Index |

The lifecycle test is the canonical end-to-end integration scenario: it seeds a Book (2 copies) and Member, then asserts the book's `AvailableCopies` after each step, confirming the service, the transaction, and the relational state stay consistent.

### 3.1 Loan controller integration

`LoanController` is driven directly against the real service and database to verify its routing and guard behaviour:

| Test | Verifies |
| :--- | :--- |
| `Create_WithDueDateBeforeLoanDate_ReturnsViewWithModelError` | service validation surfaced as a `ViewResult` + `ModelState` error |
| `Create_WithReturnDateBeforeLoanDate_ReturnsViewWithModelError` | return-date validation surfaced in the view |
| `Edit_WithMismatchedId_ReturnsBadRequest` | id-mismatch guard returns `BadRequest` |
| `Edit_WithValidData_UpdatesAndRedirects` | valid edit persists and redirects to Index |
| `Edit_WithInvalidDates_ReturnsViewWithModelError` | invalid edit re-renders the view with errors |
| `Delete_NonExistentId_ReturnsNotFound` | missing id returns `NotFound` |
| `DeleteConfirmed_WithValidId_DeletesAndRedirects` | confirmed delete removes the row and redirects |

---

## 4. Report integration

| Test | Verifies |
| :--- | :--- |
| `Index_ReturnsViewModelPopulatedFromDatabase` | `ReportController.Index` returns a `ViewResult` whose `LoanReportViewModel` is built from real DB rows |
| `Index_WithReturnedFilter_ReturnsOnlyReturnedLoans` | filter parameter flows controller → service → query |
| `ExportCsv_ReturnsCsvFileWithHeaderAndRows` | `ExportCsv` returns a `text/csv` `FileContentResult` whose bytes contain the header row and the loaned book titles |

---

## 5. Result

All **22** integration cases pass, confirming the controller, service and data-access layers integrate correctly — including EF Core transactions, cross-entity inventory updates, referential-integrity handling, controller routing/guards, relational `Include` reads, and CSV file generation.
