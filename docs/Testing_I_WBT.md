# White-Box Testing (WBT) — Testing I

**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8)
**Technique:** White-Box Testing — statement & branch coverage driven by **control-flow analysis** and **independent-path (basis-path) testing**.
**Scope:** All three functionalities — CRUD service branches, the Loan inventory engine, and the Report query builder.

White-box testing reads the source code and derives the minimum set of test cases needed to execute **every branch** of each non-trivial method.

| Functionality | Method(s) under test | Test file | Cases |
| :--- | :--- | :--- | :--- |
| CRUD (Book, Member) | `Update`/`Delete` in [BookService](../ssvv-th/Services/BookService.cs) & [MemberService](../ssvv-th/Services/MemberService.cs) | [BookMemberWbtTests.cs](../ssvv-th.Tests/WhiteBoxTests/BookMemberWbtTests.cs) | 10 |
| Loan workflow | `ValidateAsync` + `ApplyInventoryChanges` in [LoanService](../ssvv-th/Services/LoanService.cs) | [LoanWbtTests.cs](../ssvv-th.Tests/WhiteBoxTests/LoanWbtTests.cs) | 8 |
| Report | `GenerateLoanReportAsync` in [ReportService](../ssvv-th/Services/ReportService.cs) | [ReportWbtTests.cs](../ssvv-th.Tests/WhiteBoxTests/ReportWbtTests.cs) | 10 |

---

## 1. CRUD service branches

### 1.1 `BookService.DeleteAsync` / `MemberService.DeleteAsync`

```
N1  entity = Find(id)
D1  if (entity == null) → return false
N2  hasRelatedLoans = Loans.Any(fk == id)
D2  if (hasRelatedLoans) → throw InvalidOperationException
N3  Remove(entity); SaveChanges; return true
```

- Decisions = 2 → **Cyclomatic complexity V(G) = 3** → 3 independent paths.

| Path | Condition | Test (Book / Member) | Expected |
| :--- | :--- | :--- | :--- |
| P1 | entity == null | `BookDelete_NonExistentId_ReturnsFalse` / `MemberDelete_NonExistentId_ReturnsFalse` | false |
| P2 | exists, has loans | `BookDelete_WithRelatedLoans_ThrowsInvalidOperation` / `MemberDelete_WithRelatedLoans_ThrowsInvalidOperation` | throws |
| P3 | exists, no loans | `BookDelete_WithoutRelatedLoans_RemovesAndReturnsTrue` / `MemberDelete_WithoutRelatedLoans_RemovesAndReturnsTrue` | true, removed |

### 1.2 `BookService.UpdateAsync` / `MemberService.UpdateAsync`

```
D1  if (existing == null) → return null
N   copy all fields; SaveChanges; return existing
```

- Decisions = 1 → **V(G) = 2** → 2 paths.

| Path | Condition | Test | Expected |
| :--- | :--- | :--- | :--- |
| P1 | existing == null | `BookUpdate_NonExistentId_ReturnsNull` / `MemberUpdate_NonExistentId_ReturnsNull` | null |
| P2 | existing found | `BookUpdate_ExistingId_PersistsEveryField` / `MemberUpdate_ExistingId_PersistsEveryField` | every field persisted |

---

## 2. Loan inventory engine

The most complex algorithm is the stock adjustment performed when a loan is updated. Reduced control flow of `ApplyInventoryChanges`:

```
wasActive      = existingLoan.ReturnDate == null
willBeActive   = updatedLoan.ReturnDate  == null
isChangingBook = existingLoan.BookId != updatedLoan.BookId

D1  if (wasActive)      previousBook.AvailableCopies++   // releases the old hold
D2  if (willBeActive)   currentBook.AvailableCopies--    // takes the new hold
D3  if (!wasActive && !willBeActive && isChangingBook) return
```

- Decisions = 3 → **V(G) = 4**. Combined with the *same-book vs change-book* dimension this yields the following independent transitions, all covered:

| Path | wasActive | willBeActive | book change | Net effect | Test |
| :--- | :--- | :--- | :--- | :--- | :--- |
| P1 | T | F | same | old +1 | `Path_ActiveToReturned_SameBook_IncrementsCopies` |
| P2 | F | T | same | same −1 | `Path_ReturnedToActive_SameBook_DecrementsCopies` |
| P3 | T | T | same | +1 then −1 = 0 | `Path_ActiveToActive_SameBook_LeavesCopiesUnchanged` |
| P4 | F | F | same | 0 | `Path_ReturnedToReturned_SameBook_LeavesCopiesUnchanged` |
| P5 | T | T | changed | old +1, new −1 | `Path_ActiveToActive_ChangeBook_AdjustsBothInventories` |
| P6 | F | F | changed | early return, 0 | `Path_ReturnedToReturned_ChangeBook_LeavesBothInventoriesUnchanged` |

### 2.1 `ValidateAsync` guard branches

Two reachable guard branches that block invalid inventory transitions are also covered:

| Branch | Condition | Test | Expected |
| :--- | :--- | :--- | :--- |
| Reopen out-of-stock | `isReopeningLoan && AvailableCopies <= 0` | `Path_ReopenLoan_WithNoAvailableCopies_ReturnsError` | error on ReturnDate |
| Swap to out-of-stock | `isChangingBook && willBeActive && AvailableCopies <= 0` | `Path_ChangeToUnavailableBook_WhileActive_ReturnsError` | error on BookId |

> **Inspection note (see [Inspection doc](Testing_II_Inspection.md)):** the branch `if (wasActive && !willBeActive && loan.ReturnDate == null)` is **unreachable** — `!willBeActive` already implies `ReturnDate != null`. It is therefore documented as dead code rather than covered by a test.

---

## 3. Report query builder

Control flow of `GenerateLoanReportAsync`:

```
D1  if (fromDate.HasValue)  query = query.Where(LoanDate >= from)
D2  if (toDate.HasValue)    query = query.Where(LoanDate <= to)
D3-D5  switch (reportType) { Active | Overdue | Returned | default(All) }
D6  if (!IsNullOrWhiteSpace(searchTerm)) query = query.Where(title|author|name|email contains term)
    order by DueDate, then Book.Title
```

- Decisions ≈ 6 → **V(G) ≈ 7**. Branch coverage:

| Branch | Test |
| :--- | :--- |
| both date filters false + default(All) arm + ordering | `NoFilters_DefaultArm_ReturnsAllOrderedByDueDate` |
| both date filters true | `BothDateBounds_NarrowToRange` |
| switch Active / Overdue / Returned arms | `EachSwitchArm_FiltersByStatus` (3 cases) |
| search filter true — title / author / member / email sub-clauses | `SearchTerm_CoversEachMatchableField` (4 cases) |
| search filter false (whitespace) | `WhitespaceSearchTerm_SkipsFilterBranch` |

---

## 4. Result

All **28** white-box cases pass, exercising every reachable branch of the CRUD service operations, the loan inventory engine, and the report query builder. The single unreachable branch in `ValidateAsync` is reported as a defect rather than left as a coverage gap.
