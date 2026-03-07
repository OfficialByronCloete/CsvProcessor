import { describe, expect, it, vi } from 'vitest';
import type { Transaction } from '../src/models/transaction';
import { TransactionList } from '../src/components/transaction-list';

describe('transaction-list', () => {
  it('formats displayed transaction time as dd-mm-yyyy hh:mm:ss', () => {
    const sut = new TransactionList();

    const formatted = sut.formatDate('2028-09-25T14:05:09');

    expect(formatted).toBe('25-09-2028 14:05:09');
  });

  it('accepts yyyy/MM/dd transaction time and sends backend-compatible format', async () => {
    const sut = new TransactionList();
    const updateTransaction = vi.fn(async () => undefined);
    const sutWithService = sut as unknown as {
      transactionsService: { updateTransaction: (transaction: Transaction) => Promise<void> };
      loadTransactions: () => Promise<void>;
    };
    sutWithService.transactionsService = { updateTransaction };
    sutWithService.loadTransactions = vi.fn(async () => undefined);

    sut.editingTransactionId = '11111111-1111-1111-1111-111111111111';
    sut.editModel = {
      transactionId: '11111111-1111-1111-1111-111111111111',
      transactionTime: '2028/09/25',
      transactionAmount: 10.5,
      description: 'Payment',
    };

    await sut.confirmEdit();

    expect(sut.updateErrorMessage).toBe('');
    expect(updateTransaction).toHaveBeenCalledOnce();
    expect(updateTransaction).toHaveBeenCalledWith({
      transactionId: '11111111-1111-1111-1111-111111111111',
      transactionTime: '2028-09-25',
      transactionAmount: 10.5,
      description: 'Payment',
    });
  });

  it('shows specific error when transaction time is invalid during update', async () => {
    const sut = new TransactionList();
    const updateTransaction = vi.fn(async () => undefined);
    const sutWithService = sut as unknown as {
      transactionsService: { updateTransaction: (transaction: Transaction) => Promise<void> };
    };
    sutWithService.transactionsService = { updateTransaction };

    sut.editModel = {
      transactionId: '11111111-1111-1111-1111-111111111111',
      transactionTime: '\\',
      transactionAmount: 10.5,
      description: 'Payment',
    };

    await sut.confirmEdit();

    expect(sut.updateErrorMessage).toBe("'TransactionTime' must be a valid date and time value.");
    expect(updateTransaction).not.toHaveBeenCalled();
  });
});
