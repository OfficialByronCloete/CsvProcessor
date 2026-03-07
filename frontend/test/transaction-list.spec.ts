import { describe, expect, it, vi } from 'vitest';
import type { Transaction } from '../src/models/transaction';
import { TransactionList } from '../src/components/transaction-list';

describe('transaction-list', () => {
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
