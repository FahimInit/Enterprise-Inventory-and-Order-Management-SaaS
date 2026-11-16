export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ProblemDetailsError {
  title?: string;
  status?: number;
  detail?: string;
  errorCode?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export interface UserWorkspaceDto {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  baseCurrency: string;
  roleName: string;
  isOwner: boolean;
}

export interface AuthResponse {
  userId: string;
  email: string;
  fullName: string;
  isPlatformAdmin: boolean;
  activeTenantId: string | null;
  activeOrganizationName: string | null;
  activeRoleName: string | null;
  permissions: string[];
  workspaces: UserWorkspaceDto[];
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface OrganizationDto {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  baseCurrency: string;
  timeZoneId: string;
  defaultLowStockThreshold: number;
  defaultTaxRatePercent: number;
  isActive: boolean;
  activeMemberCount: number;
  createdAtUtc: string;
}

export interface OrganizationMemberDto {
  id: string;
  userId: string;
  email: string;
  fullName: string;
  roleId: string;
  roleName: string;
  isOwner: boolean;
  isActive: boolean;
  joinedAtUtc: string;
}

export interface OrganizationRoleDto {
  id: string;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  permissions: string[];
  assignedMemberCount: number;
}

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  parentCategoryId: string | null;
  parentCategoryName: string | null;
  isActive: boolean;
  productCount: number;
  createdAtUtc: string;
}

export type ProductStatus = "Draft" | "Active" | "Archived";

export interface ProductDto {
  id: string;
  categoryId: string;
  categoryName: string;
  sku: string;
  name: string;
  description: string | null;
  barcode: string | null;
  unitOfMeasure: string;
  standardCost: number;
  movingAverageCost: number;
  unitPrice: number;
  reorderPoint: number;
  targetStockLevel: number;
  status: ProductStatus;
  attributesJson: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface WarehouseDto {
  id: string;
  code: string;
  name: string;
  addressLine1: string | null;
  city: string | null;
  stateOrProvince: string | null;
  postalCode: string | null;
  country: string | null;
  managerUserId: string | null;
  managerName: string | null;
  isDefault: boolean;
  isActive: boolean;
  locationCount: number;
  createdAtUtc: string;
}

export interface WarehouseLocationDto {
  id: string;
  warehouseId: string;
  code: string;
  name: string;
  zone: string;
  isPickable: boolean;
  isReceivable: boolean;
  isActive: boolean;
}

export interface InventoryBalanceDto {
  inventoryItemId: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  categoryName: string;
  unitOfMeasure: string;
  onHandQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  inTransitQuantity: number;
  reorderPoint: number;
  targetStockLevel: number;
  isLowStock: boolean;
  movingAverageCost: number;
  totalInventoryValue: number;
  lastCountedAtUtc: string | null;
  updatedAtUtc: string | null;
}

export type StockMovementType =
  | "PurchaseReceipt"
  | "SalesFulfillment"
  | "TransferOut"
  | "TransferIn"
  | "TransferReversal"
  | "AdjustmentIncrease"
  | "AdjustmentDecrease"
  | "ReconciliationAdjustment";

export interface StockMovementDto {
  id: string;
  inventoryItemId: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  movementType: StockMovementType;
  quantityDelta: number;
  balanceBefore: number;
  balanceAfter: number;
  unitCost: number;
  referenceType: string;
  referenceNumber: string;
  referenceId: string | null;
  reason: string;
  notes: string | null;
  createdByUserId: string | null;
  occurredAtUtc: string;
}

export interface StockReservationDto {
  id: string;
  inventoryItemId: string;
  warehouseId: string;
  warehouseCode: string;
  productId: string;
  productSku: string;
  productName: string;
  salesOrderId: string;
  orderNumber: string;
  quantity: number;
  status: "Active" | "Fulfilled" | "Released";
  releaseReason: string | null;
  createdAtUtc: string;
  resolvedAtUtc: string | null;
}

export interface ReconciliationResultDto {
  reconciliationId: string;
  inventoryItemId: string;
  warehouseId: string;
  productId: string;
  productSku: string;
  systemQuantityBefore: number;
  ledgerCalculatedQuantity: number;
  countedQuantity: number;
  varianceQuantity: number;
  wasLedgerConsistentBeforeCount: boolean;
  adjustmentMovementId: string | null;
  performedAtUtc: string;
}

export interface SupplierDto {
  id: string;
  code: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  addressLine1: string | null;
  city: string | null;
  country: string | null;
  paymentTerms: string;
  leadTimeDays: number;
  isActive: boolean;
  notes: string | null;
  linkedProductCount: number;
  createdAtUtc: string;
}

export interface SupplierProductDto {
  id: string;
  supplierId: string;
  productId: string;
  productSku: string;
  productName: string;
  supplierSku: string | null;
  agreedUnitCost: number;
  minimumOrderQuantity: number;
  leadTimeDays: number;
  isPreferred: boolean;
}

export interface SupplierDetailsDto {
  supplier: SupplierDto;
  products: SupplierProductDto[];
}

export type PurchaseOrderStatus =
  | "Draft"
  | "Submitted"
  | "Approved"
  | "PartiallyReceived"
  | "Received"
  | "Cancelled";

export interface PurchaseOrderLineDto {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  orderedQuantity: number;
  receivedQuantity: number;
  remainingQuantity: number;
  unitCost: number;
  lineTotal: number;
}

export interface GoodsReceiptDto {
  id: string;
  receiptNumber: string;
  notes: string | null;
  receivedAtUtc: string;
  totalUnitsReceived: number;
}

export interface PurchaseOrderDto {
  id: string;
  orderNumber: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  targetWarehouseId: string;
  targetWarehouseCode: string;
  targetWarehouseName: string;
  status: PurchaseOrderStatus;
  subtotalAmount: number;
  taxAmount: number;
  totalAmount: number;
  expectedDeliveryDateUtc: string | null;
  notes: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  approvedAtUtc: string | null;
  completedAtUtc: string | null;
  lines: PurchaseOrderLineDto[];
  receipts: GoodsReceiptDto[];
}

export type SalesOrderStatus =
  | "Draft"
  | "Pending"
  | "Confirmed"
  | "Processing"
  | "Fulfilled"
  | "Cancelled";

export interface SalesOrderLineDto {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface SalesOrderDto {
  id: string;
  orderNumber: string;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  shippingAddress: string | null;
  fulfillmentWarehouseId: string;
  fulfillmentWarehouseCode: string;
  fulfillmentWarehouseName: string;
  status: SalesOrderStatus;
  subtotalAmount: number;
  taxRatePercent: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  notes: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  confirmedAtUtc: string | null;
  fulfilledAtUtc: string | null;
  cancelledAtUtc: string | null;
  lines: SalesOrderLineDto[];
}

export type TransferStatus =
  | "Draft"
  | "Requested"
  | "InTransit"
  | "Received"
  | "Cancelled";

export interface StockTransferLineDto {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  unitOfMeasure: string;
  quantity: number;
  notes: string | null;
}

export interface StockTransferDto {
  id: string;
  transferNumber: string;
  sourceWarehouseId: string;
  sourceWarehouseCode: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseCode: string;
  destinationWarehouseName: string;
  status: TransferStatus;
  totalQuantity: number;
  notes: string | null;
  cancellationReason: string | null;
  createdAtUtc: string;
  dispatchedAtUtc: string | null;
  receivedAtUtc: string | null;
  cancelledAtUtc: string | null;
  lines: StockTransferLineDto[];
}

export interface WarehouseValuationDto {
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  distinctProductsCount: number;
  totalOnHandUnits: number;
  totalReservedUnits: number;
  totalAvailableUnits: number;
  totalInTransitUnits: number;
  totalValuation: number;
}

export interface CategoryValuationDto {
  categoryId: string;
  categoryName: string;
  productCount: number;
  totalOnHandUnits: number;
  totalValuation: number;
}

export interface LowStockAlertItemDto {
  inventoryItemId: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productSku: string;
  productName: string;
  categoryName: string;
  onHandQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  inTransitQuantity: number;
  reorderPoint: number;
  targetStockLevel: number;
  suggestedReorderQuantity: number;
  movingAverageCost: number;
}

export interface DashboardSummaryDto {
  organizationName: string;
  baseCurrency: string;
  isDemoEnvironment: boolean;
  activeProductCount: number;
  activeCategoryCount: number;
  activeWarehouseCount: number;
  activeSupplierCount: number;
  totalOnHandUnits: number;
  totalReservedUnits: number;
  totalAvailableUnits: number;
  totalInTransitUnits: number;
  totalInventoryValuation: number;
  lowStockAlertCount: number;
  openPurchaseOrderCount: number;
  openPurchaseOrderValue: number;
  openSalesOrderCount: number;
  fulfilledSalesRevenue: number;
  pendingTransferCount: number;
  warehouseSummaries: WarehouseValuationDto[];
  topLowStockItems: LowStockAlertItemDto[];
  recentStockMovements: StockMovementDto[];
}

export interface InventoryValuationReportDto {
  baseCurrency: string;
  totalOnHandUnits: number;
  totalReservedUnits: number;
  totalAvailableUnits: number;
  totalInTransitUnits: number;
  totalInventoryValuation: number;
  byWarehouse: WarehouseValuationDto[];
  byCategory: CategoryValuationDto[];
}

export interface OrderStatusSummaryDto {
  status: string;
  orderCount: number;
  totalAmount: number;
}

export interface OrderStatisticsReportDto {
  totalPurchaseOrders: number;
  totalProcurementSpend: number;
  totalSalesOrders: number;
  totalConfirmedAndFulfilledRevenue: number;
  totalFulfilledRevenue: number;
  estimatedFulfilledCogs: number;
  estimatedGrossMargin: number;
  purchaseOrdersByStatus: OrderStatusSummaryDto[];
  salesOrdersByStatus: OrderStatusSummaryDto[];
}

export interface AuditLogDto {
  id: string;
  tenantId: string | null;
  actorUserId: string | null;
  actorEmail: string | null;
  action: string;
  entityType: string;
  entityId: string;
  summary: string;
  beforeStateJson: string | null;
  afterStateJson: string | null;
  correlationId: string | null;
  ipAddress: string | null;
  occurredAtUtc: string;
}
