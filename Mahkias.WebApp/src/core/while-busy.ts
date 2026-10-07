import { finalize, MonoTypeOperatorFunction } from 'rxjs';

/** Marks work in progress for the life of a request, including when the API errors. */
export function whileBusy<T>(setBusy: (busy: boolean) => void): MonoTypeOperatorFunction<T> {
  setBusy(true);
  return finalize(() => setBusy(false));
}
