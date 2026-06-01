# Black-Box Testing (BBT) — Testing I

**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8)
**Technique:** Black-Box Testing using **Equivalence Partitioning (EP)** and **Boundary Value Analysis (BVA)**.
**Scope:** All three functionalities — CRUD (Book, Member, Loan), the Loan borrow/return workflow, and the Report.

Black-box testing exercises each unit through its public contract (model validation rules, service inputs/outputs, report queries) **without reference to the internal source code**. Test cases are derived from the specification: required fields, field formats, length limits, date relationships, stock rules, and report filters.

| Functionality | Test file | Cases |
| :--- | :--- | :--- |
| CRUD (Book, Member) | [BookMemberBbtTests.cs](../ssvv-th.Tests/BlackBoxTests/BookMemberBbtTests.cs) | 22 |
| Loan workflow + Loan model | [LoanBbtTests.cs](../ssvv-th.Tests/BlackBoxTests/LoanBbtTests.cs) | 16 |
| Report | [ReportBbtTests.cs](../ssvv-th.Tests/BlackBoxTests/ReportBbtTests.cs) | 15 |

---

## 1. CRUD — Book & Member validation

### 1.1 Equivalence classes

| Field | Valid class | Invalid class |
| :--- | :--- | :--- |
| `Book.Title` | non-empty, length ≤ 200 | empty / length > 200 |
| `Book.Author` | non-empty, length ≤ 150 | empty |
| `Book.ISBN` | non-empty, length ≤ 20 | empty / length > 20 |
| `Book.AvailableCopies` | ≥ 0 | < 0 |
| `Member.Name` | non-empty, length ≤ 150 | empty / length > 150 |
| `Member.Email` | valid e-mail, length ≤ 150 | empty / malformed |
| `Member.Phone` | valid phone **or null** (optional) | malformed |

### 1.2 Boundary value analysis

| Variable | Boundaries tested | Expected |
| :--- | :--- | :--- |
| `Title` length | 200 (valid) / 201 (invalid) | pass / fail |
| `ISBN` length | 20 (valid) / 21 (invalid) | pass / fail |
| `Name` length | 150 (valid) / 151 (invalid) | pass / fail |
| `AvailableCopies` | −1 (invalid) / 0 (valid lower bound) / 5 (valid) | fail / pass / pass |

### 1.3 Derived test cases

| ID | Test method | Input | Expected |
| :--- | :--- | :--- | :--- |
| BBT-C01 | `Book_AllFieldsValid_PassesValidation` | valid book | no errors |
| BBT-C02 | `Book_MissingTitle_FailsValidation` | Title = "" | error on Title |
| BBT-C03 | `Book_MissingAuthor_FailsValidation` | Author = "" | error on Author |
| BBT-C04 | `Book_MissingIsbn_FailsValidation` | ISBN = "" | error on ISBN |
| BBT-C05 | `Book_TitleLengthBoundary_IsValidated` | 200 / 201 chars | pass / fail |
| BBT-C06 | `Book_IsbnLengthBoundary_IsValidated` | 20 / 21 chars | pass / fail |
| BBT-C07 | `Book_AvailableCopiesBoundary_IsValidated` | −1 / 0 / 5 | fail / pass / pass |
| BBT-C08 | `Member_AllFieldsValid_PassesValidation` | valid member | no errors |
| BBT-C09 | `Member_MissingName_FailsValidation` | Name = "" | error on Name |
| BBT-C10 | `Member_MissingEmail_FailsValidation` | Email = "" | error on Email |
| BBT-C11 | `Member_EmailFormat_IsValidated` | 2 valid / 2 invalid addresses | pass / fail |
| BBT-C12 | `Member_NameLengthBoundary_IsValidated` | 150 / 151 chars | pass / fail |
| BBT-C13 | `Member_NullPhone_IsAllowed` | Phone = null | no Phone error |
| BBT-C14 | `Member_InvalidPhone_FailsValidation` | Phone = "not a phone" | error on Phone |

---

## 2. Loan workflow

### 2.1 Equivalence classes & boundaries

| Variable | Valid class | Invalid class | Boundary |
| :--- | :--- | :--- | :--- |
| `DueDate` vs `LoanDate` | DueDate ≥ LoanDate | DueDate < LoanDate | DueDate = LoanDate (valid); LoanDate−1 (invalid) |
| `ReturnDate` vs `LoanDate` | null or ≥ LoanDate | < LoanDate | — |
| `BookId` | exists | does not exist | — |
| `MemberId` | exists | does not exist | — |
| `Book.AvailableCopies` at borrow | ≥ 1 | 0 | 1 = last copy (valid); 0 = out of stock (invalid) |
| `Loan.BookId`/`MemberId` (model `[Range]`) | ≥ 1 | 0 | 0 (invalid) / 1 (valid) |

### 2.2 Derived test cases

| ID | Test method | Input | Expected |
| :--- | :--- | :--- | :--- |
| BBT-L01 | `CreateAsync_ValidLoan_ReturnsSuccess` | valid loan, copies = 2 | success |
| BBT-L02 | `CreateAsync_DueDateEqualsLoanDate_ReturnsSuccess` | DueDate = LoanDate | success (boundary) |
| BBT-L03 | `CreateAsync_DueDateOneDayBeforeLoanDate_ReturnsError` | DueDate = LoanDate−1 | error on DueDate |
| BBT-L04 | `CreateAsync_ReturnDateBeforeLoanDate_ReturnsError` | ReturnDate < LoanDate | error on ReturnDate |
| BBT-L05 | `CreateAsync_NonExistentBook_ReturnsError` | BookId = 9999 | error on BookId |
| BBT-L06 | `CreateAsync_NonExistentMember_ReturnsError` | MemberId = 9999 | error on MemberId |
| BBT-L07 | `CreateAsync_AvailableCopiesZero_ReturnsUnavailableError` | copies = 0 | "unavailable" error |
| BBT-L08 | `CreateAsync_LastAvailableCopy_ReturnsSuccessAndReachesZero` | copies = 1 | success, copies → 0 |
| BBT-L09 | `LoanModel_BookIdRange_IsValidated` | BookId 0 / 1 | fail / pass |
| BBT-L10 | `LoanModel_MemberIdRange_IsValidated` | MemberId 0 / 1 | fail / pass |
| BBT-L11 | `LoanModel_ValidData_PassesValidation` | all fields valid | no errors |
| BBT-L12 | `LoanModel_StatusCalculation_ReturnsExpectedStatus` | ReturnDate set / DueDate past / DueDate future | Returned / Overdue / Active |

The derived `Loan.Status` property is partitioned by state: **Returned** (ReturnDate set), **Overdue** (no ReturnDate and DueDate before today), and **Active** (no ReturnDate and DueDate today or later).

---

## 3. Report

### 3.1 Equivalence classes

| Filter | Partitions |
| :--- | :--- |
| `ReportType` | All / Active / Overdue / Returned |
| `FromDate` | null (no lower bound) / set (LoanDate ≥ from) |
| `ToDate` | null (no upper bound) / set (LoanDate ≤ to) |
| `SearchTerm` | null-or-whitespace (ignored) / matches book title / matches author / matches member name / matches email / no match |

The seed dataset is three loans: one **Active** (Refactoring / Grace), one **Overdue** (Mythical Man-Month / Alan), one **Returned** (Refactoring / Alan), with controlled `LoanDate` values at today−5, today−30 and today−20.

### 3.2 Derived test cases

| ID | Test method | Filter | Expected |
| :--- | :--- | :--- | :--- |
| BBT-R01 | `Report_All_ReturnsEveryLoan` | type = All | 3 items |
| BBT-R02 | `Report_FilteredByType_ReturnsOnlyMatchingStatus` | Active / Overdue / Returned | all items match status |
| BBT-R03 | `Report_FromDate_ExcludesEarlierLoans` | from = today−10 | only LoanDate ≥ from |
| BBT-R04 | `Report_ToDate_ExcludesLaterLoans` | to = today−10 | only LoanDate ≤ to |
| BBT-R05 | `Report_FromDateOnBoundary_IsInclusive` | from = today−20 (= a loan date) | boundary loan included (2 items) |
| BBT-R06 | `Report_SearchTerm_FiltersAcrossBookAndMemberFields` | title / author / member / email / no-match | 2 / 2 / 2 / 2 / 0 |
| BBT-R07 | `Report_BlankSearchTerm_IsIgnored` | null / "   " | 3 items |
| BBT-R08 | `Report_SummaryCounts_AreComputedFromItems` | type = All | Total 3, Unique 2, Active/Overdue/Returned = 1 each |

---

## 4. Result

All **53** black-box cases pass. They confirm the system honours its specified validation rules, date relationships, inventory constraints and report filters without inspecting implementation details.
