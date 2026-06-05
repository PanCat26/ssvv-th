# Exploratory Testing Report: Loan Management Component

This document details the exploratory testing session performed on the **Loan** portion of the **Mini Library Loan Manager**, designed and executed using the **Session-Based Test Management (SBTM)** methodology.

---

## 1. Exploratory Testing Charter

- **Charter**: Explore the CRUD lifecycle of the **Loan** entity via both frontend GUI and backend API endpoints, investigating boundary conditions, date relationships, inventory state transitions, referential integrity, and negative scenarios to uncover edge cases and validation gaps.
- **Tester Role**: Senior QA Engineer (Google standard)
- **Session Duration**: 90 minutes
- **Environment**: ASP.NET Core MVC (Local Development) + SQLite / MySQL local instance
- **Heuristics Applied**:
  - **CRUD Operations**: Normal, valid sequences (Create -> View -> Update -> Delete).
  - **Constraint Violations**: Attempting to bypass limits (e.g. borrowing unavailable books).
  - **Date Boundary Tests**: Checking extremes, reversals, and empty values.
  - **Referential Integrity & Cascades**: Deleting associated entities (Book/Member) with active loans.
  - **API Bypass**: Directly posting raw HTTP payload data to controller endpoints to bypass UI restrictions.
  - **Time Travel / Status Transitions**: Verifying "Active", "Returned", and "Overdue" dynamic calculations.

---

## 2. Session Log & Scenario Executions

Below is the record of the specific exploratory scenarios executed, along with their outcomes.

### Scenario A: The Happy Path CRUD Lifecycle
- **Procedure**:
  1. Create a Book (Title: "Clean Code", Author: "Robert C. Martin", ISBN: "9780132350884", Available Copies: 5).
  2. Create a Member (Name: "John Doe", Email: "john.doe@example.com", Phone: "555-0199").
  3. Navigate to `/Loan/Create`, select "John Doe" and "Clean Code", keep default dates (LoanDate = Today, DueDate = Today + 14 days), click **Save**.
  4. Edit the loan to set a ReturnDate (Today + 2 days), click **Save**.
  5. Delete the loan from `/Loan/Delete`.
- **Expected Outcome**: Loan is created successfully, visible in `/Loan` index page, editable, status changes from "Active" to "Returned", and successfully deleted.
- **Actual Outcome**:
  - Loan created successfully.
  - Status correctly displayed as **Active**.
  - Editing Return Date succeeded, status updated to **Returned**.
  - Deletion succeeded.
- **QA Notes**: The happy path works, but the inventory count of "Clean Code" remained at 5 throughout the entire lifecycle (Defect #1, #2, #3).

---

### Scenario B: Date Reversals and Boundaries
- **Procedure**:
  1. Navigate to `/Loan/Create`.
  2. Attempt to save a loan where `DueDate` is *before* `LoanDate` (e.g., Loan Date = 2026-06-15, Due Date = 2026-06-10).
  3. Attempt to save a loan where `ReturnDate` is *before* `LoanDate` (e.g., Loan Date = 2026-06-15, Return Date = 2026-06-14).
  4. Attempt to enter extreme past/future dates (e.g., Year 1000 or Year 9999).
- **Expected Outcome**:
  - Step 2: System rejects with "Due date cannot be before the loan date."
  - Step 3: System rejects with "Return date cannot be before the loan date."
  - Step 4: System enforces reasonable range boundaries.
- **Actual Outcome**:
  - Step 2: Form submission failed, rendering a validation error (Passed).
  - Step 3: Form submission failed, rendering a validation error (Passed).
  - Step 4: Allowed without error, writing year `0001` or `9999` to the database (Failed).

---

### Scenario C: Borrowing Out-of-Stock Books
- **Procedure**:
  1. Create a Book (Title: "Test-Driven Development", Available Copies: 0).
  2. Create a Member (Name: "Alice Smith").
  3. Navigate to `/Loan/Create`, select "Alice Smith" and "Test-Driven Development", click **Save**.
- **Expected Outcome**: System prevents selection of "Test-Driven Development" (disables or marks as out-of-stock) OR rejects the form with a clear message: *"Book unavailable."*
- **Actual Outcome**:
  - The book was visible and fully selectable in the dropdown list.
  - The loan was created successfully.
  - No errors were shown, and the book's copy count remained at 0 in the Book index page.
- **QA Notes**: CRITICAL bug. Violates the primary business rule.

---

### Scenario D: Deleting Members/Books with Active Loans
- **Procedure**:
  1. Create a Book ("Refactoring"), Member ("Bob"), and a Loan linking them.
  2. Navigate to `/Book` list, click **Delete** on "Refactoring", and confirm deletion.
  3. Repeat for Member "Bob" in the `/Member` list.
- **Expected Outcome**: The system prevents deleting "Refactoring" or "Bob" because of their active loans, displaying a friendly banner message: *"Cannot delete this entity because it has active loans."*
- **Actual Outcome**:
  - The browser page crashed, showing a blank page / raw ASP.NET Developer Exception page with a `DbUpdateException` (Foreign Key constraint failed).
- **QA Notes**: Severe UX defect. Backend DbContext enforces referential integrity on delete (`OnDelete(DeleteBehavior.Restrict)`), but the controllers do not catch the exception to present it gracefully in the GUI.

---

### Scenario E: API Payload Injection / Bypassing GUI Drops
- **Procedure**:
  1. Intercept or construct a direct HTTP POST request to `/Loan/Create`.
  2. Populate the form payload with:
     - `BookId`: `99999` (non-existent book)
     - `MemberId`: `88888` (non-existent member)
     - `LoanDate`: `2026-05-31`
     - `DueDate`: `2026-06-14`
  3. Submit the raw HTTP POST request to `http://localhost:5223/Loan/Create`.
- **Expected Outcome**: The server rejects the post with a `400 Bad Request` or renders form validation errors explaining that the selected Book and Member do not exist.
- **Actual Outcome**:
  - Server accepted the POST request, failed on database insert due to Foreign Key constraint violation, crashing with a raw database exception (Internal Server Error 500).
- **QA Notes**: Demonstrates a security/robustness vulnerability where arbitrary numeric IDs can crash the server.

---

### Scenario F: Dynamic "Status" Calculation (Time Travel)
- **Procedure**:
  1. Create a loan with a `DueDate` set to yesterday, and `ReturnDate` set to `null`. Verify status.
  2. Edit the loan, setting `ReturnDate` to today. Verify status.
  3. Create a loan with `DueDate` set to tomorrow, and `ReturnDate` set to `null`. Verify status.
- **Expected Outcome**:
  - Step 1: Status is **Overdue**.
  - Step 2: Status is **Returned**.
  - Step 3: Status is **Active**.
- **Actual Outcome**: All statuses updated and displayed correctly (Passed).
- **QA Notes**: The dynamic status calculation property in the `Loan` model is functionally sound.

---

## 3. Exploratory Bugs Summary

We consolidated the findings from this exploratory session into four key bugs:

| Bug ID | Title | Severity | Component | Reproduction |
| :--- | :--- | :--- | :--- | :--- |
| **E-01** | Infinite borrowing of out-of-stock books | **HIGH (Critical)** | Service/UI | Select book with `AvailableCopies = 0` during Loan creation. |
| **E-02** | Crash on deleting Book/Member with active loan | **MEDIUM** | Controller/UI | Attempt to delete Book/Member that is currently referenced in an active Loan. |
| **E-03** | Server 500 error when submitting invalid IDs | **MEDIUM** | Controller/API | POST payload with fake `BookId` or `MemberId` to `/Loan/Create`. |
| **E-04** | Extreme date entries permitted | **LOW** | UI/Validation | Enter `LoanDate` as `0001-01-01` or `9999-12-31`. |

In the next phase, we will construct our **Automated Test Project** to programmatically reproduce, verify, and validate these exact scenarios.
