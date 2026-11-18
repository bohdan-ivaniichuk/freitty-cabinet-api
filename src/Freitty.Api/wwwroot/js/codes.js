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

// The ProblemDetails code of a write made against an older version of the order.
export const VERSION_CONFLICT = 'order.version_conflict';
