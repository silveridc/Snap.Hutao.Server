/** OpenAPI 原始文档里我们用到的部分，字段全部可选，兼容 2.0 与 3.x */
export interface RawSchema {
  $ref?: string;
  type?: string;
  format?: string;
  title?: string;
  description?: string;
  enum?: unknown[];
  default?: unknown;
  example?: unknown;
  nullable?: boolean;
  deprecated?: boolean;
  properties?: Record<string, RawSchema>;
  items?: RawSchema;
  additionalProperties?: RawSchema | boolean;
  required?: string[];
  allOf?: RawSchema[];
  oneOf?: RawSchema[];
  anyOf?: RawSchema[];
  'x-enum-varnames'?: string[];
}

export interface RawParameter {
  name: string;
  in: string;
  required?: boolean;
  description?: string;
  schema?: RawSchema;
  type?: string;
  format?: string;
  enum?: unknown[];
  default?: unknown;
}

export interface RawMediaType {
  schema?: RawSchema;
  example?: unknown;
  examples?: Record<string, unknown>;
}

export interface RawResponse {
  description?: string;
  content?: Record<string, RawMediaType>;
  schema?: RawSchema;
  headers?: Record<string, RawSchema & { description?: string }>;
}

export interface RawOperation {
  tags?: string[];
  summary?: string;
  description?: string;
  operationId?: string;
  deprecated?: boolean;
  parameters?: RawParameter[];
  requestBody?: { description?: string; required?: boolean; content?: Record<string, RawMediaType> };
  responses?: Record<string, RawResponse>;
  security?: Record<string, unknown>[];
  consumes?: string[];
  produces?: string[];
}

export interface RawPathItem {
  parameters?: RawParameter[];
  get?: RawOperation;
  put?: RawOperation;
  post?: RawOperation;
  delete?: RawOperation;
  options?: RawOperation;
  head?: RawOperation;
  patch?: RawOperation;
  trace?: RawOperation;
}

export interface RawDocument {
  openapi?: string;
  swagger?: string;
  info?: { title?: string; version?: string; description?: string };
  host?: string;
  basePath?: string;
  schemes?: string[];
  tags?: { name: string; description?: string }[];
  servers?: { url: string; description?: string }[];
  paths?: Record<string, RawPathItem>;
  definitions?: Record<string, RawSchema>;
  components?: {
    schemas?: Record<string, RawSchema>;
    securitySchemes?: Record<string, RawSecurityScheme>;
  };
  securityDefinitions?: Record<string, RawSecurityScheme>;
  security?: Record<string, unknown>[];
}

export interface RawSecurityScheme {
  type?: string;
  scheme?: string;
  name?: string;
  in?: string;
  description?: string;
  flows?: Record<string, unknown>;
}

/** 归一化后的模型 */
export interface Field {
  name: string;
  label: string;
  type: string;
  ref?: string;
  required: boolean;
  nullable: boolean;
  description: string;
  enumValues: string[];
  defaultValue: string;
  example: string;
  children: Field[];
  recursive: boolean;
}

export interface ParamItem {
  name: string;
  location: string;
  required: boolean;
  description: string;
  type: string;
  enumValues: string[];
  defaultValue: string;
  example: string;
}

export interface ContentBlock {
  contentType: string;
  fields: Field[];
  ref: string;
  schema: RawSchema | null;
}

export interface ResponseBlock {
  status: string;
  description: string;
  contents: ContentBlock[];
  headers: { name: string; type: string; description: string }[];
}

export interface Operation {
  id: string;
  docKey: string;
  method: string;
  path: string;
  tag: string;
  summary: string;
  description: string;
  operationId: string;
  deprecated: boolean;
  params: ParamItem[];
  requestContents: ContentBlock[];
  requestBodyRequired: boolean;
  responses: ResponseBlock[];
  securityNames: string[];
}

export interface ModelDef {
  name: string;
  type: string;
  description: string;
  fields: Field[];
  enumValues: string[];
  usedBy: string[];
}

export interface SecurityScheme {
  name: string;
  type: string;
  scheme: string;
  in: string;
  headerName: string;
  /** apiKey + Authorization + 描述里提到 Bearer：按 Bearer 前缀处理 */
  bearerHint: boolean;
  description: string;
}

export interface NormalizedDoc {
  meta: {
    title: string;
    version: string;
    description: string;
    baseUrl: string;
    specVersion: string;
  };
  groups: { tag: string; description: string; operations: Operation[] }[];
  operations: Operation[];
  models: ModelDef[];
  securitySchemes: SecurityScheme[];
  warnings: string[];
}
