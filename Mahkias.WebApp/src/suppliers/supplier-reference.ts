export function supplierReferenceKey(value?: string | null): string {
  return (value ?? '').trim().toLowerCase();
}

export function duplicateSupplierReferenceMessage(reference: string): string {
  return `Supplier reference "${reference.trim()}" is already in use.`;
}

export function hasDuplicateSupplierReference(
  reference: string,
  suppliers: Array<{ id: number; reference?: string | null }>,
  exceptId?: number,
): boolean {
  const key = supplierReferenceKey(reference);
  if (!key) {
    return false;
  }
  return suppliers.some((supplier) => supplier.id !== exceptId && supplierReferenceKey(supplier.reference) === key);
}
