# Freitty client cabinet

The language of the client cabinet of a cross-docking platform: a client company orders hub services
for its freight and follows them through Dashboard, Order List and Order Detail.

## Language

### Parties and places

**Company**:
The client organization that owns a cabinet, places **Orders** and pays for them. The hub's screens
call it the customer.
_Avoid_: Customer, client, account, tenant

**User**:
A person who works in the cabinet on behalf of one **Company**, as an Admin or a Dispatcher. Hub
staff and drivers are not **Users**.
_Avoid_: Member, operator

**Hub**:
A cross-dock facility of the platform where an **Order** is handled.
_Avoid_: Warehouse, terminal, location

**Transfer hub**:
A second **Hub** that the freight of an **Order** is moved on to, arranged by platform staff. It is
not the **Destination**.
_Avoid_: Destination hub

**Destination**:
The city and region where the freight of an **Order** finally goes after it leaves the **Hub**.
_Avoid_: Delivery address, consignee

**Carrier**:
A trucking company that moves freight. An **Order** names a **Carrier** only when its
**Transport** is of that kind.
_Avoid_: Transporter, trucker

**Transport**:
How the freight of an **Order** leaves the hub: with a **Carrier**, with the **Company**'s own
driver, or by self pickup — the receiver comes for it.
_Avoid_: Delivery method, shipping

### Order and its cargo

**Order**:
A client's request for a hub to handle freight. Once submitted, an **Order** has one or more
**Shipments**.

**Cross-Dock**:
An **Order** with one **Shipment** that is unloaded, handled and loaded out. Its kind says what happens
in between: Storage — it waits at the hub; Pickup — it is collected from the hub. The Pickup kind
is not self pickup, which is a kind of **Transport**.

**Consolidation**:
An **Order** that combines several **Shipments** into shared outbound trailers.

**Draft number**:
The number an **Order** carries while it is a **Draft** (`DRAFT-003`). It belongs to the
**Company** and is replaced by the official order number (`FR001684`) on submission.

**Scheduled date**:
When the freight of an **Order** is expected at the **Hub** — the "Date" of a card.
_Avoid_: Due date, delivery date

**Shipment**:
One consignment of freight inside an **Order**, declared on one bill of lading and identified by the
client's **Ref N**. A Cross-Dock has exactly one; a Consolidation has at least two, which the UI
lists as sub-orders (`FR001676-1`).
_Avoid_: Sub-order (UI label only), order reference, order line

**Ref N**:
The client's own reference number for a **Shipment** (`REF-1001`). An attribute of a **Shipment**,
not an entity.
_Avoid_: Reference number, PO number

**Cargo Line**:
One line of a bill of lading: a **Unit Type** and the quantity declared for it. A **Shipment** has
one **Cargo Line** per **Unit Type**.
_Avoid_: Order item, pallet line

**Unit Type**:
The kind of handling unit a quantity is counted in — Standard (48×40) or XL.
_Avoid_: Pallet type, unit (the form's label "Unit" only)

**Declared quantity**:
What the bill of lading says should arrive, per **Cargo Line**. The declared quantity of a
**Shipment** or an **Order** is always a sum of **Cargo Lines**, never a separate number.
_Avoid_: Expected quantity, planned quantity

### Order lifecycle

The status of an **Order** describes only what happens to the freight. Payment is a fact of an
**Invoice**, and an **Alert** is not a status.

**Draft**:
An **Order** the client is still filling in. The hub does not see it.

**Ready**:
An **Order** the client has submitted and the hub has not started. The UI labels it "New".
_Avoid_: New, submitted, pending

**In progress**:
An **Order** the hub has started work on and has not completed. Any action of the hub starts it.

**Completed**:
An **Order** the hub has finished: every **Shipment** is **Received**, nothing is left
**On stock** and no **Alert** is open. The mockup also labels it "Done" and "Closed"; the cabinet
shows "Completed".
_Avoid_: Done, closed, paid

**Active**:
An **Order** that is **Ready** or **In progress** — the "Active Orders" of the dashboard.
_Avoid_: Open (that word is for an **Alert**)

**Stage**:
Where the freight of an **Order** is in the hub's work, derived from its status and its
**Shipments**: Draft, Awaiting truck, Unloading, Loading, Awaiting completion or Completed. Unlike
the status, it tells the hub's work apart; an **Alert** does not change it.
_Avoid_: Status (the status has four values and does not say what the hub is doing)

**Next step**:
What an **Order** card says happens next: an action of the client — continue editing a **Draft**,
upload a photo, review a **Quantity delta** — the hub's next **Stage**, or, for a **Completed**
**Order**, payment of its **Invoice**. "Closed" is the next step of a **Completed** **Order**
without an **Invoice**, not a status.

### What the hub does

**Arrival**:
The inbound vehicle of a **Shipment** — trailer type, truck and trailer number — and the
moment it came in. It is not the **Transport** of the **Order**, which is outbound.
_Avoid_: Check-in, gate-in

**Dock assignment**:
Where the hub works a **Shipment**: a dock, a bay and the floor lead responsible. It can
change.
_Avoid_: Door, slot

**Operation**:
One physical action the hub performed on the freight of a **Shipment** — Unloading,
Disposal, Restack or Loading — with a **Unit Type** and a quantity. The **Operations** of an
**Order** are the only record of what actually happened to its freight. A Restack rearranges pallets
and does not change their count. "Applied at" is the moment an **Operation** was recorded.
_Avoid_: Action, activity, movement

**Actual quantity**:
What the hub received, per **Shipment** and **Unit Type**: the sum of its Unloading
**Operations**. Never entered on its own.
_Avoid_: Received count, warehouse quantity

**On stock**:
What is physically at the hub now: unloaded minus disposed minus loaded.
_Avoid_: Balance, remaining

**Received**:
A **Shipment** whose receiving the hub has declared finished. Until then freight may still
be arriving.
_Avoid_: Arrived, unloaded, closed

### What needs the client's attention

**Quantity delta**:
The difference between **Actual quantity** and **Declared quantity** of a **Shipment**, per
**Unit Type**. An excess is a delta as soon as it is unloaded; a shortage is a delta only once the
shipment is **Received** — freight that is still arriving is not short.
_Avoid_: Discrepancy, variance, mismatch

**Alert**:
A record that the client was told about a problem with one **Shipment**, from the moment it
was raised until it was resolved. An **Order** "has an alert" while any of its shipments has an
open one. An **Alert** is not a status of the **Order**.
_Avoid_: Warning, issue, flag

**Quantity delta alert**:
The **Alert** raised when the **Quantity delta** of a **Shipment** appears or changes. It
ends when the quantities match or when someone resolves it, accepting the difference as final; the
same difference does not raise it again.

**Missing photo alert**:
The **Alert** raised when an **Operation** that documents the condition of freight — Unloading or
Disposal — was recorded without a photo. It ends when someone resolves it.

**Awaiting action**:
**Orders** that only the client can move: **Drafts** and **Orders** with an unpaid **Invoice**.
_Avoid_: Pending

**Need attention**:
What waits for the client, the dashboard tile and the number on the Orders badge: the **Orders**
**Awaiting action** plus the **Orders** with an open **Alert**. An **Order** that is both is
counted in each.

### What the client pays for

**Service**:
A kind of work from the platform's catalog that the client selects for an **Order** — Transload,
Restock & Rework.
_Avoid_: Option, add-on

**Supply item**:
A stock item in the platform's catalog — straps, corners, shrink wrap — with its current price.
_Avoid_: Material, consumable, product

**Supply line**:
A quantity of one **Supply item** sold on an **Order**, at the price the item had when it was
added.
_Avoid_: Order supply, purchase

**Invoice**:
A request for payment issued to a **Company** for one **Order**. Whether an **Order** is paid is a
fact of its **Invoice**, never of its status.
_Avoid_: Bill, payment

### Reporting

**Reporting time zone**:
The one time zone in which the cabinet decides where a day, a week or a month begins — for "today"
and "this week" of the Date filter and for the periods of **Activity**. A single event is still
shown in the local time of its **Hub**.
_Avoid_: Local time, user's time zone

**Activity**:
The dashboard's chart of **Orders** completed and of spend — the amounts of the **Invoices**
issued — per day, week, month or quarter. Not the **Operations** of an **Order**.

## Relationships

- A **Company** owns many **Orders**; a **User** of that **Company** creates each one.
- A submitted **Order** has one or more **Shipments**: exactly one for a Cross-Dock, at least two
  for a Consolidation. A **Draft** may have none yet.
- A **Shipment** has one **Cargo Line** per **Unit Type**, one **Arrival**, one current
  **Dock assignment**, and its own **Operations** and **Alerts**.
- An **Order** is handled at one **Hub** and may have a **Transfer hub**.
- An **Order** has many **Supply lines** and selects many **Services**.
- An **Order** may have several **Invoices**; an **Invoice** belongs to one **Order**.

## Example dialogue

> **Dev:** FR001383 shows "Actual 12 · Δ +2". Is 12 a number someone typed in?
>
> **Domain expert:** No. The hub recorded an Unloading **Operation** of twelve pallets for its
> **Shipment**. **Actual quantity** is the sum of Unloadings.
>
> **Dev:** And the **Declared quantity**, ten — that is on the **Order**?
>
> **Domain expert:** On the **Cargo Line** of the shipment, as on the bill of lading. The order has
> no quantity of its own.
>
> **Dev:** So the order is in status "Alert"?
>
> **Domain expert:** There is no such status. The order is **In progress**. Its shipment has an
> open **Quantity delta alert**, and the card shows that instead of the status.
>
> **Dev:** If only eight had come, would the alert be raised after the Unloading too?
>
> **Domain expert:** Not yet — the rest may be on the next truck. A shortage counts once the hub
> says the shipment is **Received**. An excess counts at once.
>
> **Dev:** The two extra pallets stay. Does the **Alert** stay open forever?
>
> **Domain expert:** Someone resolves it — the difference is accepted as final. The **Quantity
> delta** of plus two stays visible; only then can the hub complete the order.
>
> **Dev:** And once it is **Completed**, is it "Closed" when the client pays?
>
> **Domain expert:** Payment is the **Invoice**'s business. **Completed** means the freight is gone
> from the hub.

## Flagged ambiguities

- **"Sub-order" and "ref"** name the same rows in the mockup. Resolved: the concept is a
  **Shipment**; "sub-order" is only the UI label for a **Shipment** of a Consolidation.
- **"Customer"** on the detail screen is the **Company** that owns the order, not a third party.
- **"New", "Done", "Closed", "Paid"** appear as status labels. Resolved: the statuses are **Ready**
  and **Completed**; "Paid" is not a status at all.
- **"Alert"** appears where a status badge would be. Resolved: it is a record on a **Shipment**,
  shown in place of the status while open.
- **"Actual"** is what was received, not what is at the hub now — that is **On stock**.
- **"Pickup"** is a kind of Cross-Dock; **"Self pickup"** is a kind of **Transport**. An order can
  be both, and neither implies the other.
- **"Carrier" and "Truck / trailer"** sit together on the detail screen but are different things:
  the **Carrier** takes the freight away, the truck and trailer of the **Arrival** brought it in.
