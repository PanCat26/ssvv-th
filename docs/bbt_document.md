# Black Box Testing (BBT) Document

## 1. Overview
This document details the Black Box Testing applied to the `CreateAsync` method of the `LoanService`. The goal is to validate the input/output behavior of the service without looking at the internal source code, focusing on business rules and data validation.

## 2. Equivalence Partitioning (EP)
We partitioned the input data into valid and invalid classes based on the business requirements for creating a loan.

### Input Variable: `DueDate`
- **Valid Class**: `DueDate >= LoanDate`
- **Invalid Class**: `DueDate < LoanDate`

### Input Variable: `ReturnDate`
- **Valid Class**: `ReturnDate >= LoanDate` or `null`
- **Invalid Class**: `ReturnDate < LoanDate`

### Input Variable: `BookId` & `MemberId` (Foreign Keys)
- **Valid Class**: Exists in the database.
- **Invalid Class**: Does not exist in the database.

### Input Variable: `AvailableCopies` (Book Stock)
- **Valid Class**: `> 0`
- **Invalid Class**: `<= 0`

## 3. Boundary Value Analysis (BVA)
BVA focuses on the edge cases of our Equivalence Classes, specifically for dates and inventory integers.

- **Inventory Boundary**: 
  - `AvailableCopies = 1` (Valid Boundary - last copy)
  - `AvailableCopies = 0` (Invalid Boundary - out of stock)

## 4. Derived Test Cases

Based on EP and BVA, the following test cases were derived and implemented in `LoanServiceBbtTests.cs`:

| Test Case ID | Test Name | Input Description | Expected Output | Status |
| :--- | :--- | :--- | :--- | :--- |
| **TC-BBT-01** | `CreateAsync_ValidLoan_ReturnsSuccess` | Valid Loan Data, AvailableCopies > 0 | `Succeeded = true`, No Errors | Passed |
| **TC-BBT-02** | `CreateAsync_DueDateBeforeLoanDate_ReturnsError` | `DueDate` is exactly 1 day before `LoanDate` | `Succeeded = false`, Error on `DueDate` | Passed |
| **TC-BBT-03** | `CreateAsync_ReturnDateBeforeLoanDate_ReturnsError` | `ReturnDate` is exactly 1 day before `LoanDate` | `Succeeded = false`, Error on `ReturnDate` | Passed |
| **TC-BBT-04** | `CreateAsync_NonExistentBook_ReturnsError` | `BookId = 999` (Invalid) | `Succeeded = false`, Error on `BookId` | Passed |
| **TC-BBT-05** | `CreateAsync_NonExistentMember_ReturnsError` | `MemberId = 999` (Invalid) | `Succeeded = false`, Error on `MemberId` | Passed |
| **TC-BBT-06** | `CreateAsync_BookUnavailable_ReturnsError` | `AvailableCopies = 0` (Invalid Boundary) | `Succeeded = false`, Error on `BookId` | Passed |
