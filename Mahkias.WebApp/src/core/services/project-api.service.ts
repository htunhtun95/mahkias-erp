import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

export interface Project {
  id: number;
  name: string;
  reference: string;
  description: string;
  createdAt?: string | null;
  modifiedAt?: string | null;
  activities?: Activity[];
}

export interface Activity {
  id: number;
  projectId: number;
  partNo: string;
  mainPartNo?: string | null;
  alternativePartNos?: string[];
  budget: number | null;
  description: string | null;
  dsnNo: string | null;
  quantity: number | null;
  typeId: number | null;
  type: string | null;
  createdAt?: string | null;
  modifiedAt?: string | null;
  activityGroupId?: number | null;
  activityGroupName?: string | null;
  quoteReceived?: number;
  deliveryProgress?: number;
}

export interface ActivityGroup {
  id: number;
  projectId: number;
  name: string;
  description?: string | null;
  typeId?: number | null;
  createdAt?: string | null;
}

export interface ActivityType {
  id: number;
  name: string;
  slug: string;
}

export interface CreateProjectRequest {
  name: string;
  reference: string;
  description: string;
}

export interface CreateActivityRequest {
  partNo: string;
  budget: number | null;
  description: string;
  dsnNo: string;
  quantity: number | null;
  typeId: number | null;
}

export interface ExcelColumn {
  letter: string;
  header: string | null;
}

export interface ActivityExcelMapping {
  columns: ExcelColumn[];
  rowCount: number;
  suggested: {
    dsnNo?: string;
    partNo?: string;
    description?: string;
    type?: string;
    quantity?: string;
    budget?: string;
  };
}

export interface ActivityImportIssue {
  row: number;
  field: string;
  message: string;
}

export interface ActivityImportValidation {
  valid: boolean;
  ready: number;
  rowCount: number;
  errorCount: number;
  message?: string;
  errors: ActivityImportIssue[];
}

export interface Supplier {
  id: number;
  name: string;
  reference?: string | null;
  createdAt?: string | null;
  modifiedAt?: string | null;
  quotationCount?: number;
}

export interface SupplierQuotation {
  id: number;
  code: string;
  projectNames?: string | null;
  projectId?: number | null;
  itemCount: number;
  createdAt?: string | null;
}

export interface SupplierDetail extends Supplier {
  projectCount: number;
  quotations: SupplierQuotation[];
}

export interface QuotationListItem {
  id: number;
  supplierId: number;
  supplierName: string;
  code: string;
  supplierReference?: string | null;
  createdAt?: string | null;
  modifiedAt?: string | null;
  itemCount: number;
  totalPrice: number;
}

export interface QuotationLine {
  id: number;
  dsnNo?: string | null;
  partNo?: string | null;
  description?: string | null;
  unitPrice?: number | null;
  quantity: number;
  totalPrice?: number | null;
  activityId?: number | null;
  mappedActivity?: string | null;
}

export interface QuotationDetail extends QuotationListItem {
  items: QuotationLine[];
}

export interface ActivityQuotationLink {
  activityId: number;
  itemId?: number;
  quotationId: number;
  quotationRef: string;
  supplierName: string;
  supplierQuotationRef?: string | null;
  partNo?: string | null;
  description?: string | null;
  quantity?: number | null;
  alternativePart?: boolean;
  unitPrice?: number | null;
  totalPrice?: number | null;
  lowest?: boolean;
  recordedAt?: string | null;
}

export interface QuotationItemInput {
  dsnNo: string;
  partNo: string;
  description: string;
  unitPrice: number | null;
  quantity: number;
  totalPrice: number | null;
  activityId?: number | null;
  activityGroupId?: number | null;
  row?: number;
  priceWarning?: boolean;
}

export interface QuotationMapCandidate {
  kind: 'activity' | 'group';
  id: number;
  label: string;
}

export interface QuotationMapChoice {
  row: number;
  dsnNo?: string | null;
  partNo?: string | null;
  description?: string | null;
  unitPrice?: number | null;
  quantity?: number | null;
  totalPrice?: number | null;
  priceWarning?: boolean;
  candidates: QuotationMapCandidate[];
  target?: string;
}

export interface QuotationExcelMapping {
  columns: ExcelColumn[];
  rowCount: number;
  suggested: {
    dsnNo?: string;
    partNo?: string;
    description?: string;
    unitPrice?: string;
    quantity?: string;
    totalPrice?: string;
    supplierReference?: string;
  };
}

export interface QuotationImportValidation {
  valid: boolean;
  ready: number;
  rowCount: number;
  errorCount: number;
  message?: string;
  errors: ActivityImportIssue[];
  items: QuotationItemInput[];
  choices?: QuotationMapChoice[];
  supplierReference?: string | null;
}

export interface ActivityExcelUpload {
  dsnNoColumn: string;
  partNoColumn: string;
  descriptionColumn: string;
  typeColumn: string;
  quantityColumn: string;
  budgetColumn: string;
  defaultDsnNo: string;
  defaultPartNo: string;
  defaultTypeId: number | null;
  defaultQuantity: number | null;
  defaultBudget: number | null;
}

@Injectable({ providedIn: 'root' })
export class ProjectApiService {
  private readonly baseUrl = `${environment.endPoints.systemApiEndPoint}api`;

  constructor(private http: HttpClient) {}

  list(search?: string, sortBy = 'createdAt', sortDirection: 'asc' | 'desc' = 'desc') {
    const params: Record<string, string> = { sortBy, sortDirection };
    if (search?.trim()) {
      params['search'] = search.trim();
    }
    return this.http.get<Project[]>(`${this.baseUrl}/projects`, { params });
  }

  get(id: number, sortBy = 'createdAt', sortDirection: 'asc' | 'desc' = 'desc') {
    return this.http.get<Project>(`${this.baseUrl}/projects/${id}`, {
      params: { sortBy, sortDirection },
    });
  }

  create(body: CreateProjectRequest) {
    return this.http.post<Project>(`${this.baseUrl}/projects`, body);
  }

  update(id: number, body: CreateProjectRequest) {
    return this.http.put<Project>(`${this.baseUrl}/projects/${id}`, body);
  }

  deleteProject(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/projects/${id}`);
  }

  updateActivity(id: number, body: CreateActivityRequest & { projectId: number }) {
    return this.http.put<Activity>(`${this.baseUrl}/activities/${id}`, body);
  }

  updateActivities(
    activities: Array<CreateActivityRequest & { id: number; projectId: number }>,
  ) {
    return this.http.put<{ updated: number }>(`${this.baseUrl}/activities/batch`, { activities });
  }

  deleteActivity(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/activities/${id}`);
  }

  activityGroups(projectId: number) {
    return this.http.get<ActivityGroup[]>(`${this.baseUrl}/projects/${projectId}/activity-groups`);
  }

  createActivityGroup(projectId: number, name: string, description: string) {
    return this.http.post<ActivityGroup>(`${this.baseUrl}/projects/${projectId}/activity-groups`, { name, description });
  }

  updateActivityGroup(projectId: number, id: number, name: string, description: string) {
    return this.http.put<ActivityGroup>(`${this.baseUrl}/projects/${projectId}/activity-groups/${id}`, { name, description });
  }

  deleteActivityGroup(projectId: number, id: number) {
    return this.http.delete<void>(`${this.baseUrl}/projects/${projectId}/activity-groups/${id}`);
  }

  assignActivityGroup(projectId: number, activityId: number, activityGroupId: number | null) {
    return this.http.put<void>(`${this.baseUrl}/projects/${projectId}/activities/${activityId}/group`, { activityGroupId });
  }

  assignActivities(projectId: number, activityIds: number[], activityGroupId: number | null) {
    return this.http.put<{ updated: number }>(`${this.baseUrl}/projects/${projectId}/activity-groups/assign`, {
      activityIds,
      activityGroupId,
    });
  }

  addActivity(projectId: number, body: CreateActivityRequest) {
    return this.http.post<Activity>(`${this.baseUrl}/projects/${projectId}/activities`, body);
  }

  bulkActivities(projectId: number, activities: CreateActivityRequest[]) {
    return this.http.post<{ inserted: number }>(`${this.baseUrl}/projects/${projectId}/activities/bulk`, {
      activities,
    });
  }

  excelMapping(projectId: number, file: File) {
    const body = new FormData();
    body.append('file', file);
    return this.http.post<ActivityExcelMapping>(`${this.baseUrl}/projects/${projectId}/activities/excel/mapping`, body);
  }

  excelValidate(projectId: number, file: File, mapping: ActivityExcelUpload) {
    return this.http.post<ActivityImportValidation>(
      `${this.baseUrl}/projects/${projectId}/activities/excel/validate`,
      this.excelForm(file, mapping),
    );
  }

  excelUpload(projectId: number, file: File, mapping: ActivityExcelUpload) {
    return this.http.post<{ inserted: number }>(
      `${this.baseUrl}/projects/${projectId}/activities/excel/upload`,
      this.excelForm(file, mapping),
    );
  }

  private excelForm(file: File, mapping: ActivityExcelUpload) {
    const body = new FormData();
    body.append('file', file);
    body.append('dsnNoColumn', mapping.dsnNoColumn);
    body.append('partNoColumn', mapping.partNoColumn);
    body.append('descriptionColumn', mapping.descriptionColumn);
    body.append('typeColumn', mapping.typeColumn ?? '');
    body.append('quantityColumn', mapping.quantityColumn ?? '');
    body.append('budgetColumn', mapping.budgetColumn ?? '');
    body.append('defaultDsnNo', mapping.defaultDsnNo ?? '');
    body.append('defaultPartNo', mapping.defaultPartNo ?? '');
    if (mapping.defaultTypeId) {
      body.append('defaultTypeId', String(mapping.defaultTypeId));
    }
    if (mapping.defaultQuantity != null) {
      body.append('defaultQuantity', String(mapping.defaultQuantity));
    }
    if (mapping.defaultBudget != null) {
      body.append('defaultBudget', String(mapping.defaultBudget));
    }
    return body;
  }

  activityTypes() {
    return this.http.get<ActivityType[]>(`${this.baseUrl}/lookup/activity-types`);
  }

  suppliers(search?: string) {
    const params: Record<string, string> = {};
    if (search?.trim()) {
      params['search'] = search.trim();
    }
    return this.http.get<Supplier[]>(`${this.baseUrl}/suppliers`, { params });
  }

  createSupplier(name: string, reference?: string) {
    return this.http.post<Supplier>(`${this.baseUrl}/suppliers`, { name, reference: reference ?? null });
  }

  supplier(id: number) {
    return this.http.get<SupplierDetail>(`${this.baseUrl}/suppliers/${id}`);
  }

  updateSupplier(id: number, name: string, reference?: string) {
    return this.http.put<SupplierDetail>(`${this.baseUrl}/suppliers/${id}`, { name, reference: reference ?? null });
  }

  deleteSupplier(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/suppliers/${id}`);
  }

  quotations(projectId: number) {
    return this.http.get<QuotationListItem[]>(`${this.baseUrl}/projects/${projectId}/quotations`);
  }

  quotation(id: number) {
    return this.http.get<QuotationDetail>(`${this.baseUrl}/quotations/${id}`);
  }

  quotationLinks(projectId: number) {
    return this.http.get<ActivityQuotationLink[]>(`${this.baseUrl}/projects/${projectId}/quotation-links`);
  }

  updateQuotation(id: number, body: {
    supplierId: number;
    supplierReference: string;
    projectIds: number[];
    items: QuotationItemInput[];
  }) {
    return this.http.put<QuotationDetail>(`${this.baseUrl}/quotations/${id}`, body);
  }

  deleteQuotation(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/quotations/${id}`);
  }

  nextQuotationCode() {
    return this.http.get<{ code: string }>(`${this.baseUrl}/quotations/next-code`);
  }

  createQuotation(body: {
    supplierId: number;
    supplierReference: string;
    projectIds: number[];
    items: QuotationItemInput[];
    duplicateHandling?: 'merge' | 'choose' | 'separate';
  }) {
    return this.http.post<QuotationListItem>(`${this.baseUrl}/quotations`, body);
  }

  quotationExcelMapping(projectId: number, file: File) {
    const body = new FormData();
    body.append('file', file);
    return this.http.post<QuotationExcelMapping>(
      `${this.baseUrl}/projects/${projectId}/quotations/excel/mapping`,
      body,
    );
  }

  quotationExcelValidate(
    projectId: number,
    file: File,
    mapping: {
      dsnNoColumn: string;
      partNoColumn: string;
      descriptionColumn: string;
      unitPriceColumn: string;
      quantityColumn: string;
      totalPriceColumn: string;
      defaultDsnNo: string;
      defaultPartNo: string;
      defaultDescription: string;
      defaultUnitPrice: string;
      defaultQuantity: string;
      defaultTotalPrice: string;
      duplicateHandling?: string;
    },
  ) {
    const body = new FormData();
    body.append('file', file);
    body.append('dsnNoColumn', mapping.dsnNoColumn ?? '');
    body.append('partNoColumn', mapping.partNoColumn ?? '');
    body.append('descriptionColumn', mapping.descriptionColumn ?? '');
    body.append('unitPriceColumn', mapping.unitPriceColumn ?? '');
    body.append('quantityColumn', mapping.quantityColumn ?? '');
    body.append('totalPriceColumn', mapping.totalPriceColumn ?? '');
    body.append('defaultDsnNo', mapping.defaultDsnNo ?? '');
    body.append('defaultPartNo', mapping.defaultPartNo ?? '');
    body.append('defaultDescription', mapping.defaultDescription ?? '');
    body.append('defaultUnitPrice', mapping.defaultUnitPrice ?? '');
    body.append('defaultQuantity', mapping.defaultQuantity ?? '');
    body.append('defaultTotalPrice', mapping.defaultTotalPrice ?? '');
    body.append('duplicateHandling', mapping.duplicateHandling ?? 'choose');
    return this.http.post<QuotationImportValidation>(
      `${this.baseUrl}/projects/${projectId}/quotations/excel/validate`,
      body,
    );
  }
}
