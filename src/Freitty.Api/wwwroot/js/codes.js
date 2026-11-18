// The codes of the API that the scripts compare against or send, named once. format.js words them.

// In the order an order goes through them.
export const STATUS = {
  Draft: 'Draft',
  Ready: 'Ready',
  InProgress: 'InProgress',
  Completed: 'Completed',
};

export const ORDER_TYPE = { CrossDock: 'CrossDock', Consolidation: 'Consolidation' };

export const ALERT_TYPE = { QuantityDelta: 'QuantityDelta', MissingPhoto: 'MissingPhoto' };

// In the order the form offers them; the first is its default.
export const OPERATION_TYPE = {
  Unloading: 'Unloading',
  Disposal: 'Disposal',
  Restack: 'Restack',
  Loading: 'Loading',
};

// In the order the form offers them; the first is its default.
export const UNIT_TYPE = { Standard: 'Standard', ExtraLarge: 'ExtraLarge' };

export const TRANSPORT_KIND = { Carrier: 'Carrier', OwnDriver: 'OwnDriver', SelfPickup: 'SelfPickup' };

export const ROLE = { Admin: 'Admin', Dispatcher: 'Dispatcher' };

export const PERIOD = { Day: 'Day', Week: 'Week', Month: 'Month', Quarter: 'Quarter' };

// What an upload of photos may hold, as the API checks it; the page checks the same before sending.
// The API reads the type from the bytes, the page only from what the browser says.
export const PHOTO_UPLOAD = {
  maxFiles: 5,
  maxSizeBytes: 5 * 1024 * 1024,
  contentTypes: ['image/jpeg', 'image/png', 'image/webp'],
};

// The ProblemDetails code of a write made against an older version of the order.
export const VERSION_CONFLICT = 'order.version_conflict';

// The ProblemDetails code of an order number that names no order — also a draft number after the
// draft was submitted.
export const ORDER_NOT_FOUND = 'order.not_found';
