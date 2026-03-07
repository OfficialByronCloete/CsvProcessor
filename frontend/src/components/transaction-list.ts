import type { Transaction } from '../models/transaction';
import { TransactionsService } from '../services/transactions-service';
import type { CsvImportResult } from '../models/csv-import-result';

export class TransactionList {
  private readonly transactionsService = new TransactionsService();
  private static readonly invalidTransactionTimeMessage = "'TransactionTime' must be a valid date and time value.";

  public transactions: Transaction[] = [];
  public selectedFiles: FileList | null = null;
  public editModel: Transaction | null = null;
  public pageNumber = 1;
  public pageSize = 10;
  public totalCount = 0;
  public isLoading = false;
  public isUploading = false;
  public deletingTransactionId = '';
  public updatingTransactionId = '';
  public editingTransactionId = '';
  public errorMessage = '';
  public uploadSuccessMessage = '';
  public uploadErrorMessage = '';
  public deleteErrorMessage = '';
  public updateErrorMessage = '';

  public get selectedFile(): File | null {
    return this.selectedFiles?.item(0) ?? null;
  }

  public get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  public async attached(): Promise<void> {
    await this.loadTransactions();
  }

  public async previousPage(): Promise<void> {
    if (this.pageNumber <= 1 || this.isLoading) {
      return;
    }

    this.pageNumber--;
    await this.loadTransactions();
  }

  public async nextPage(): Promise<void> {
    if (this.pageNumber >= this.totalPages || this.isLoading) {
      return;
    }

    this.pageNumber++;
    await this.loadTransactions();
  }

  public formatAmount(amount: number): string {
    return amount.toFixed(2);
  }

  public formatDate(value: string): string {
    return new Date(value).toLocaleString();
  }

  public async uploadSelectedFile(): Promise<void> {
    if (this.isUploading || this.isLoading) {
      return;
    }

    const file = this.selectedFile;
    this.uploadSuccessMessage = '';
    this.uploadErrorMessage = '';

    if (file === null) {
      this.uploadErrorMessage = 'Please select a CSV file to upload.';
      return;
    }

    if (!file.name.toLowerCase().endsWith('.csv')) {
      this.uploadErrorMessage = 'Invalid file type. Only CSV files are allowed.';
      return;
    }

    this.isUploading = true;
    try {
      const result = await this.transactionsService.uploadTransactionsCsv(file);
      this.uploadSuccessMessage = this.getUploadSummaryMessage(result);
      this.selectedFiles = null;
      this.pageNumber = 1;
      await this.loadTransactions();
    } catch (error) {
      this.uploadErrorMessage = error instanceof Error ? error.message : 'Failed to upload CSV.';
    } finally {
      this.isUploading = false;
    }
  }

  private getUploadSummaryMessage(result: CsvImportResult): string {
    if (result.message) {
      return result.message;
    }

    const added = result.addedCount ?? 0;
    const duplicates = result.duplicateCount ?? 0;
    return added === 0
      ? `No new transactions were added. Duplicates found: ${duplicates}.`
      : `Upload complete. Added ${added} transaction(s). Duplicates found: ${duplicates}.`;
  }

  public async deleteTransaction(transactionId: string): Promise<void> {
    if (this.isLoading || this.isUploading || this.deletingTransactionId || this.updatingTransactionId || this.editingTransactionId) {
      return;
    }

    this.deleteErrorMessage = '';
    this.deletingTransactionId = transactionId;
    try {
      await this.transactionsService.deleteTransaction(transactionId);
      if (this.transactions.length === 1 && this.pageNumber > 1) {
        this.pageNumber--;
      }

      await this.loadTransactions();
    } catch (error) {
      this.deleteErrorMessage = error instanceof Error ? error.message : 'Failed to delete transaction.';
    } finally {
      this.deletingTransactionId = '';
    }
  }

  public startEditing(transaction: Transaction): void {
    if (this.isLoading || this.isUploading || this.deletingTransactionId || this.updatingTransactionId) {
      return;
    }

    this.updateErrorMessage = '';
    this.editingTransactionId = transaction.transactionId;
    this.editModel = { ...transaction };
  }

  public discardEdit(): void {
    if (this.updatingTransactionId) {
      return;
    }

    this.editModel = null;
    this.editingTransactionId = '';
    this.updateErrorMessage = '';
  }

  public async confirmEdit(): Promise<void> {
    if (this.isLoading || this.isUploading || this.deletingTransactionId || this.updatingTransactionId || this.editModel === null) {
      return;
    }

    const amount = Number(this.editModel.transactionAmount);
    if (!this.editModel.transactionTime.trim()) {
      this.updateErrorMessage = 'Transaction time is required.';
      return;
    }

    if (!this.isValidTransactionTime(this.editModel.transactionTime)) {
      this.updateErrorMessage = TransactionList.invalidTransactionTimeMessage;
      return;
    }

    if (!this.editModel.description.trim()) {
      this.updateErrorMessage = 'Description is required.';
      return;
    }

    if (!Number.isFinite(amount)) {
      this.updateErrorMessage = 'Amount must be numeric.';
      return;
    }

    this.updateErrorMessage = '';
    this.updatingTransactionId = this.editModel.transactionId;
    try {
      await this.transactionsService.updateTransaction({
        ...this.editModel,
        transactionAmount: amount,
      });
      this.editModel = null;
      this.editingTransactionId = '';
      await this.loadTransactions();
    } catch (error) {
      this.updateErrorMessage = error instanceof Error ? error.message : 'Failed to update transaction.';
    } finally {
      this.updatingTransactionId = '';
    }
  }

  private async loadTransactions(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';

    try {
      const result = await this.transactionsService.getTransactions(this.pageNumber, this.pageSize);
      this.transactions = result.data;
      this.pageNumber = result.pageNumber;
      this.pageSize = result.pageSize;
      this.totalCount = result.totalCount;
    } catch (error) {
      this.transactions = [];
      this.totalCount = 0;
      this.errorMessage = error instanceof Error ? error.message : 'Failed to load transactions.';
    } finally {
      this.isLoading = false;
    }
  }

  private isValidTransactionTime(value: string): boolean {
    return !Number.isNaN(Date.parse(value));
  }
}
