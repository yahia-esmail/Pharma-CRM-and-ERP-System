# PharmaERP: Sales and Field Force Management for Pharmaceutical Companies

PharmaERP connects **the representative in the field** with **management in the office**, in one system. It covers:
- visits;
- orders;
- collections;
- custody;
- warehouses;
- returns;
- purchasing;
- expenses;
- targets.

Every action is recorded as it happens, with a location, a time and the name of the person who did it.

---

## The problem it solves

At most pharmaceutical companies, field work is followed up like this:
- **Visit reports.** Reps send them over WhatsApp or on paper at the end of the day, and nobody can be sure the visit actually happened.
- **Orders.** They are taken over the phone, re-keyed by hand, and arrive late.
- **Cash and stock with reps.** They are tracked in Excel sheets, so discrepancies surface too late.
- **The overall picture.** Management only sees it at the end of the month, when it is too late to correct anything.

**PharmaERP turns this around.** Managers see what is happening today, not what happened last month.

---

## Who it is for

**Companies:**
- **Manufacturers** of pharmaceuticals, cosmetics and supplements whose medical reps visit doctors.
- **Pharmaceutical agents and distributors** whose sales reps sell to pharmacies and collect payment from them.
- **Companies that do both:** medical promotion with doctors, plus selling and collecting at pharmacies.

It suits teams of anywhere from **10 to several hundred reps**, spread over territories and district managers.

**Users inside the company:**

| Role | What they use it for |
|---|---|
| **Representative** | A phone app for the day's plan, visits, orders, collections, returns, expenses, and their own custody |
| **District manager** | Their district's dashboard, approving visit plans, following reps on a map, reviewing flagged visits |
| **Sales manager** | Approving orders, targets and performance, customers and credit |
| **Senior management** | One executive dashboard summarising every department |
| **Finance** | Reviewing collections and receipts, settling reps' cash custody, expenses, supplier payments due |
| **Warehouse** | Stock, batches and expiry dates, issuing custody to reps, receiving returns |
| **Procurement** | Suppliers, purchase orders, goods receipts |
| **System administrator** | Users and permissions, plus a full log of every change |

Each role sees only what concerns it.

---

## Features

### The representative's app (on the phone)
- **Install:** opens from a link and installs on the home screen of Android and iPhone in seconds, with no app store.
- **Offline:** reps keep working in basements and areas with poor coverage. Everything is saved on the phone and sent automatically when the connection returns, **with nothing lost and nothing recorded twice**.
- **Today's plan:** visits in order, on a map and in a list, each marked done, next or missed.
- **Location-verified visits:** reps check in and out at the customer with GPS, and the system compares their position with the customer's saved location. If they are too far away, they must give a reason, and the visit is flagged for their manager.
- **Orders:** taken during the visit, with products and discounts. They can be saved as drafts and submitted for approval.
- **Photo evidence:** a photo of the receipt for collections, and of the invoice for **returns** and **expenses**.
- **Custody:** the stock and cash the rep holds, with every movement listed.
- **Notifications:** sent instantly when an order or an expense is approved or rejected, even while the app is closed.
- **Sign-in:** with a fingerprint or face instead of a password.

### Customers
- **Doctors.** Each has a specialty and a status (prospect, active, inactive). Each also has a classification, for example A, B or C, and every classification sets a required number of visits per month.
- **Pharmacies.** Each has a **credit limit** and **payment terms**. The system works out every pharmacy's balance and overdue amount, and blocks the approval of any order that would take the balance over the limit.
- **Customer lists.** Lists can be static or update automatically. Customers can be moved between reps in bulk, and every transfer is logged.
- **Follow-ups.** A call, a visit or a sample delivery, with a due date and an owner.
- **Correcting customer locations.** A rep proposes the correct location from where they stand, and a manager approves it.

### Visit plans
- The rep or a manager prepares the plan, and the district manager approves it.
- An alert fires when a planned visit isn't logged.
- **Coverage is measured:** visits actually made against the number required by each doctor's classification.

### Sales and orders
- The full order cycle: draft, then approval or rejection with a reason, then fulfilment.
- Each sale is linked to the product, batch, rep and customer.

### Collections and custody
- **Payment methods:** cash, cheque, bank transfer or card.
- **Allocation:** a payment is applied to open invoices oldest first, or to specific invoices.
- **Finance review** of collections against the receipt photo.
- **Cash custody settlement** between the rep and the cashier.
- **Stock custody counts.** The rep counts what they hold, and any difference from the balance in the system shows up at once. The difference is settled with a documented movement.
- An alert fires when custody has stayed with a rep for too long.

### Warehouses and products
- Multiple warehouses, and **batches with expiry dates**.
- Alerts for **low stock** and **approaching expiry**.
- Stock issued to reps as custody and received back from them, with every movement recorded.

### Returns
- **Two flows:** pharmacy to rep, and rep to warehouse.
- **Every return needs a reason** (expired, damaged, unsold or other) and goes through approval.
- **Batch check:** the system confirms that the returned batch is the one that was actually sold.

### Purchasing and suppliers
- Purchase orders, and goods received in full or in part, by batch.
- Supplier payments, with alerts when they fall due.

### Expenses
- Reps record expenses such as transport and client entertainment with a photo of the invoice. A manager approves or rejects each one.

### Performance and targets
- A monthly target per rep for sales, collections and visit coverage.
- Achievement is shown live, not at the end of the month.

### Tracking and control
- **A map for managers:** where reps made their visits, and their route during working hours.
- **Flagged visits:** a visit is flagged when it was:
  - too far from the customer;
  - too short;
  - made with a phone clock that was set by hand;
  - outside the rep's territory.
- **Trace any transaction** from the supplier's purchase order, through the warehouse, the rep's custody, the sale and the collection, to the cash handed in.
- **A complete change log:** who changed what, and when.

### Dashboards
- **Executive dashboard:** every department summarised on one page.
- **District manager dashboard:** drills down from district to rep to customer.
- **Field pilot report:** how well the app is working in the field for each rep, exportable to Excel.

---

## Why it is easy to use

- **Reps don't need long training.** The screens follow the rep's day: plan, then visit, then order. The buttons are large and work one-handed. An [English user guide](docs/field-app-guide.md) and an [Arabic one](docs/field-app-guide.ar.md) are available for reps.
- **Nothing to download or update from a store.** Updates reach every phone automatically, and the rep taps **Update** once they have finished what they are doing.
- **No work is lost.** If the connection drops, the phone switches off or the app is closed, the data is kept and sent later.
- **Everything is connected.**
  - An order changes the pharmacy's balance.
  - A collection settles its invoices.
  - A return goes back into stock.

  Nothing is entered twice.
- **Management works in the browser,** with no software to install on office computers.

---

## Getting the most out of it

### 1. Get the data right
The system is only as good as its data:
- **Customer locations.** If they aren't recorded, have reps record them on their first visit, and review the proposals.
- **Doctor classifications,** and the visits required for each.
- **Credit limits and payment terms** for every pharmacy.
- **Opening custody balances** for every rep, from a physical count.

### 2. Start with a one-week pilot
- Choose **3 to 5 reps** from different areas, including one with weak coverage.
- Check the **Field Pilot report** every day for:
  - location accuracy and speed;
  - flagged visits;
  - visits left open.
- Fix the causes before the full rollout: wrong customer locations, phone settings, or training.

### 3. Roll out in stages
Go territory by territory, with each district manager leading the rollout in their own district.

### 4. Make it a routine

| Who | Daily | Weekly | Monthly |
|---|---|---|---|
| **District manager** | Approve plans, review flagged visits | Coverage per rep, planned visits that weren't made | Review reps against their targets |
| **Sales manager** | Approve orders | Customers near their credit limit or overdue | Set new targets |
| **Finance** | Review collections against receipts | Settle cash custody | Reconcile accounts, debt ageing |
| **Warehouse** | Issue custody, receive returns | Approaching expiry, low stock | Count stock custody with reps |
| **Management** | — | Executive dashboard | Compare territories and performance |

### 5. Use the alerts
The system raises alerts on its own for:
- orders or expenses awaiting approval;
- planned visits that weren't logged;
- low stock or approaching expiry;
- custody held by a rep for too long;
- supplier payments falling due.

Have each person deal with their alerts as they arrive, instead of searching for problems themselves.

### The value you will see
- **Real visits:** every visit carries a location and a time, and coverage is measured in numbers.
- **Faster orders:** an order is sent from the pharmacy and approved the same day.
- **Less cash in the field:** collections are backed by receipts, and custody is settled as you go.
- **Less waste:** expiry alerts, plus batch tracing on returns.
- **Faster decisions:** management sees the numbers every day, not at the end of the month.

---

## For the technical team
- Deployment and configuration: [deploy/README.md](deploy/README.md)
- Representative's guide: [docs/field-app-guide.md](docs/field-app-guide.md)
