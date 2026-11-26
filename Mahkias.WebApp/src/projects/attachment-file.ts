export type AttachmentTone = 'pdf' | 'sheet' | 'doc' | 'image' | 'file';

export function attachmentExtension(fileName: string | null | undefined): string {
  const name = fileName ?? '';
  const index = name.lastIndexOf('.');
  return index >= 0 ? name.slice(index + 1).toLowerCase() : '';
}

export function attachmentIcon(fileName: string | null | undefined): string {
  const kind = attachmentExtension(fileName);
  if (kind === 'pdf') {
    return 'picture_as_pdf';
  }
  if (kind === 'xls' || kind === 'xlsx' || kind === 'xlsm' || kind === 'csv') {
    return 'table_chart';
  }
  if (kind === 'png' || kind === 'jpg' || kind === 'jpeg' || kind === 'gif' || kind === 'webp' || kind === 'bmp' || kind === 'tif' || kind === 'tiff') {
    return 'image';
  }
  if (kind === 'doc' || kind === 'docx') {
    return 'description';
  }
  return 'draft';
}

export function attachmentTone(fileName: string | null | undefined): AttachmentTone {
  const kind = attachmentExtension(fileName);
  if (kind === 'pdf') {
    return 'pdf';
  }
  if (kind === 'xls' || kind === 'xlsx' || kind === 'xlsm' || kind === 'csv') {
    return 'sheet';
  }
  if (kind === 'doc' || kind === 'docx') {
    return 'doc';
  }
  if (kind === 'png' || kind === 'jpg' || kind === 'jpeg' || kind === 'gif' || kind === 'webp' || kind === 'bmp' || kind === 'tif' || kind === 'tiff') {
    return 'image';
  }
  return 'file';
}
