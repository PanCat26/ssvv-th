# White Box Testing (WBT) Document

## 1. Overview
This document details the White Box Testing applied to the `UpdateAsync` method and its internal `ApplyInventoryChanges` logic within `LoanService`. WBT requires full access to the source code to ensure that every logical path and branch executes correctly.

## 2. Algorithm Under Test
The most complex algorithm is how the system handles inventory stock when a loan is updated. A loan can change its state from Active to Returned, or the actual borrowed Book can change.

```csharp
bool wasActive = existingLoan.ReturnDate == null;
bool willBeActive = updatedLoan.ReturnDate == null;
bool isChangingBook = existingLoan.BookId != updatedLoan.BookId;

// Path 1: Returning a book
if (wasActive && !willBeActive) { oldBook.Copies++; }

// Path 2: Re-opening a loan
if (!wasActive && willBeActive) { oldBook.Copies--; }

// Path 3: Swapping books on an active loan
if (wasActive && willBeActive && isChangingBook) { oldBook.Copies++; newBook.Copies--; }
```

## 3. Control Flow Graph (CFG) & Cyclomatic Complexity
- **Nodes (N)**: 6 (Start, Eval Path 1, Eval Path 2, Eval Path 3, Apply Changes, End)
- **Edges (E)**: 7
- **Cyclomatic Complexity (V(G))**: `E - N + 2` = 7 - 6 + 2 = **3 independent paths** (plus variations for book swaps).

We target **100% Statement and Branch Coverage**.

## 4. Independent Paths & Test Cases

We derived exactly the paths needed to traverse every single `if` statement and boundary. These are implemented in `LoanServiceWbtTests.cs`.

| Path | Test Name | Variable States | Action Triggered | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Path 1** | `UpdateAsync_ActiveToReturned_SameBook` | `wasActive=T`, `willBeActive=F` | Increments inventory (+1) | Passed |
| **Path 2** | `UpdateAsync_ReturnedToActive_SameBook` | `wasActive=F`, `willBeActive=T` | Decrements inventory (-1) | Passed |
| **Path 3** | `UpdateAsync_ActiveToActive_SameBook` | `wasActive=T`, `willBeActive=T`, `isChangingBook=F` | No changes (0) | Passed |
| **Path 4** | `UpdateAsync_ReturnedToReturned_SameBook` | `wasActive=F`, `willBeActive=F` | No changes (0) | Passed |
| **Path 5** | `UpdateAsync_ActiveToActive_ChangeBook` | `wasActive=T`, `willBeActive=T`, `isChangingBook=T` | Adjusts both inventories (+1, -1) | Passed |

## 5. Conclusion
By executing these 5 tests, every single logical branch and line of code inside the inventory calculation engine is verified, achieving 100% path coverage for this module.
