# Inspection & Code Review Report: Loan Management Component

This document details the formal code inspection and review conducted on the **Loan** portion (models, services, controllers, and views) of the **Mini Library Loan Manager** application, following senior QA standards at Google.

---

## 1. Inspection Checklist

The codebase was evaluated against the following standards:

| ID | Category | Checklist Item | Status | Comments |
| :--- | :--- | :--- | :--- | :--- |
| **C-01** | **Business Logic** | Are all business requirements in the specification implemented correctly? | **FAILED** | The core requirement to decrease available copies when borrowing a book and prevent loans for out-of-stock books is missing. |
| **C-02** | **Business Logic** | Does returning a book (setting `ReturnDate`) correctly release the copy? | **FAILED** | Returning a book has no effect on `AvailableCopies`. |
| **C-03** | **Data Integrity** | Are `BookId` and `MemberId` validated for existence before creating/updating a loan? | **FAILED** | Direct database inserts are attempted without existence checks, risking raw foreign key constraint crashes. |
| **C-04** | **Input Validation**| Are boundary conditions for dates handled (e.g. due date < loan date)? | **PASSED** | Custom validation in `ValidateDates` blocks this scenario. |
| **C-05** | **Input Validation**| Are negative values or default date anomalies properly validated? | **FAILED** | Users can input extreme past/future dates. |
| **C-06** | **Security** | Are controllers protected against Cross-Site Request Forgery (CSRF/XSRF)? | **PASSED** | `[ValidateAntiForgeryToken]` is present on all POST endpoints, and ASP.NET Core forms automatically inject tokens. |
| **C-07** | **Error Handling** | Are database exceptions (e.g., duplicate entries, integrity violations) caught gracefully? | **FAILED** | Controller actions lack `try-catch` blocks for DB operations, leading to raw 500 error pages. |
| **C-08** | **UI/UX** | Does the frontend provide clear visual indicators for invalid input or unselectable items (e.g. unavailable books)? | **FAILED** | Dropdowns list all books, regardless of available copies, without showing copy counts. |

---

## 2. Detailed Defect Log

Below is the list of defects identified during the code inspection of the Loan component.

### Defect #1: Missing Inventory Decrement on Loan Creation (CRITICAL)
- **Component**: Backend Service / Controller
- **File**: [LoanService.cs](file:///c:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th/Services/LoanService.cs#L33-L38)
- **Description**: The core business rule from `th.md` states: *"If the book has available copies: create loan, decrease available copies. Else: show error: book unavailable."*
  Neither `LoanService.CreateAsync` nor `LoanController.Create` checks for copy availability or decrements `AvailableCopies` upon loan creation.
- **Severity**: **HIGH (Critical)**
- **Impact**: Database inventory count becomes completely out of sync with actual loans. Users can create infinite loans for a single book copy.
- **Recommended Fix**:
  Implement transaction-protected check-and-decrement logic:
  ```csharp
  var book = await _context.Books.FindAsync(loan.BookId);
  if (book == null) throw new InvalidOperationException("Book not found.");
  if (book.AvailableCopies <= 0) throw new InvalidOperationException("Book is unavailable.");
  book.AvailableCopies--;
  _context.Loans.Add(loan);
  await _context.SaveChangesAsync();
  ```

### Defect #2: Missing Inventory Increment on Loan Return (HIGH)
- **Component**: Backend Service / Controller
- **File**: [LoanService.cs](file:///c:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th/Services/LoanService.cs#L40-L54)
- **Description**: When a loan is updated to mark it as returned (i.e. `ReturnDate` changes from `null` to a valid date), the system does not increment the book's `AvailableCopies`.
- **Severity**: **HIGH**
- **Impact**: Books marked as "Returned" are permanently lost from available stock unless manually edited by an administrator.
- **Recommended Fix**:
  In `UpdateAsync`, check if `existing.ReturnDate == null && loan.ReturnDate != null`. If so, increment the associated book's `AvailableCopies` by 1.

### Defect #3: Missing Inventory Increment on Loan Deletion (HIGH)
- **Component**: Backend Service
- **File**: [LoanService.cs](file:///c:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th/Services/LoanService.cs#L56-L65)
- **Description**: Deleting an active (unreturned) loan should restore the available copy count of the book, but it currently does not.
- **Severity**: **HIGH**
- **Impact**: Loss of book stock in system inventory when active loans are cancelled or deleted.
- **Recommended Fix**:
  In `DeleteAsync`, if the loan has `ReturnDate == null`, increment `Book.AvailableCopies` by 1 before saving changes.

### Defect #4: Lack of Foreign Key Existence Check in Controller (MEDIUM)
- **Component**: Backend Controller
- **File**: [LoanController.cs](file:///c:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th/Controllers/LoanController.cs#L41-L54)
- **Description**: In the POST `Create` and `Edit` actions, the controller does not verify if the submitted `BookId` and `MemberId` exist in the database. If an attacker or api client posts an invalid ID, EF Core will trigger a raw database constraint violation exception (`MySqlException` or `DbUpdateException`), resulting in an unhandled 500 error page.
- **Severity**: **MEDIUM**
- **Impact**: Application crashes and exposes server-side details (via stack trace in development) when bad API requests are received.
- **Recommended Fix**:
  Perform check prior to save:
  ```csharp
  var book = await _bookService.GetByIdAsync(loan.BookId);
  var member = await _memberService.GetByIdAsync(loan.MemberId);
  if (book == null) ModelState.AddModelError(nameof(loan.BookId), "Selected book does not exist.");
  if (member == null) ModelState.AddModelError(nameof(loan.MemberId), "Selected member does not exist.");
  ```

### Defect #5: Zero-Copy Books Displayed in Select Dropdowns (LOW / UX)
- **Component**: Frontend View / Controller
- **File**: [LoanController.cs](file:///c:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th/Controllers/LoanController.cs#L123-L135)
- **Description**: The book selection dropdown in `/Loan/Create` and `/Loan/Edit` displays all books in alphabetical order, even if `AvailableCopies == 0`. There is no visual indicator (e.g. appending " - OUT OF STOCK" to the text, or disabling the option).
- **Severity**: **LOW**
- **Impact**: Frustrated users who select a book, submit the form, and get a validation error (or successfully borrow it due to the missing backend checks).
- **Recommended Fix**:
  Update `PopulateDropdownsAsync` to include the copy count in the display string, and optionally disable out-of-stock books or append an warning:
  ```csharp
  ViewBag.Books = new SelectList(
      books.Select(b => new { 
          b.Id, 
          Display = $"{b.Title} ({b.Author}) [Copies: {b.AvailableCopies}]" 
      }),
      "Id", "Display", loan?.BookId);
  ```

### Defect #6: Missing Graceful Exception Handling on Delete Actions (MEDIUM)
- **Component**: Backend Controller
- **File**: [LoanController.cs](file:///c:/Users/user/Documents/0_Facultate/SEM6/SSVV/Exam/TakeHomeExam/ssvv-th/Controllers/LoanController.cs#L101-L112)
- **Description**: If a deletion fails because of database connectivity issues or sudden constraint locks, the server crashes with a 500 error.
- **Severity**: **MEDIUM**
- **Impact**: Poor recovery from sudden failures.
- **Recommended Fix**:
  Wrap DB deletions in a `try-catch` block and return a user-friendly error message via `TempData` or `ModelState`.

---

## 3. Summary of Code Review Findings

Our inspection confirms that the backend suffers from a **fundamental architectural omission**: it lacks the core business logic governing physical inventory. While the project is set up cleanly using an MVC pattern, the code was left as a basic database CRUD wrapper without service-level transaction safety or validation rules for inventory tracking.

In the next step, we will design our **Exploratory Testing Charter** to verify these defects empirically and explore edge cases in the application's runtime behavior.
