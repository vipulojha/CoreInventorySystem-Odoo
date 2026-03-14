const { asyncHandler, sendJson } = require("../../lib/api");
const { withClient } = require("../../lib/db");

const VALID_TRANSITIONS = {
  Draft:   "Ready",
  Waiting: "Ready",
  Ready:   "Done",
};

// POST /api/inventory/transition
// Body: { ref, action: "advance" | "cancel" }
module.exports = asyncHandler(async (req, res) => {
  if (req.method !== "POST") { res.statusCode = 405; return res.end(); }

  const body = typeof req.body === "object" ? req.body : JSON.parse(req.body || "{}");
  const { ref, action } = body;

  if (!ref || !action) return sendJson(res, 400, { ok: false, message: "ref and action are required." });
  if (!["advance", "cancel"].includes(action)) return sendJson(res, 400, { ok: false, message: "action must be 'advance' or 'cancel'." });

  await withClient(async (client) => {
    // Load operation
    const opRes = await client.query(
      `select id, status, operation_type, warehouse_id, from_location_id, to_location_id from inventory_operation where reference = $1`,
      [ref]
    );
    if (opRes.rows.length === 0) throw new Error("Operation not found.");
    const op = opRes.rows[0];

    if (["Done", "Cancelled"].includes(op.status)) throw new Error(`Operation is already ${op.status}.`);

    let newStatus;
    if (action === "cancel") {
      newStatus = "Cancelled";
    } else {
      newStatus = VALID_TRANSITIONS[op.status];
      if (!newStatus) throw new Error(`Cannot advance from status '${op.status}'.`);
    }

    // If advancing to Done → log stock moves and update balances
    if (newStatus === "Done") {
      const linesRes = await client.query(
        `select ol.id, ol.product_id, ol.quantity from inventory_operation_line ol where ol.operation_id = $1`,
        [op.id]
      );

      for (const line of linesRes.rows) {
        const moveKind = op.operation_type === "Receipt" ? "IN"
                        : op.operation_type === "Delivery" ? "OUT"
                        : "ADJUST";

        // Insert stock move
        await client.query(`
          insert into stock_move
            (operation_id, operation_line_id, reference, product_id, warehouse_id, from_location_id, to_location_id, quantity, move_kind, performed_by_user_id, note)
          values ($1,$2,$3,$4,$5,$6,$7,$8,$9,1,'Validated via Vercel app')
        `, [op.id, line.id, ref, line.product_id, op.warehouse_id, op.from_location_id, op.to_location_id, line.quantity, moveKind]);

        // Update stock_balance
        if (moveKind === "IN") {
          await client.query(`
            insert into stock_balance (warehouse_id, location_id, product_id, on_hand, allocated)
            values ($1, $2, $3, $4, 0)
            on conflict (warehouse_id, location_id, product_id)
            do update set on_hand = stock_balance.on_hand + $4, updated_at = now()
          `, [op.warehouse_id, op.to_location_id, line.product_id, line.quantity]);
        } else if (moveKind === "OUT") {
          await client.query(`
            update stock_balance
            set on_hand = greatest(0, on_hand - $1), updated_at = now()
            where warehouse_id=$2 and location_id=$3 and product_id=$4
          `, [line.quantity, op.warehouse_id, op.from_location_id, line.product_id]);
        }
      }
    }

    await client.query(
      `update inventory_operation set status=$1, updated_at=now() where id=$2`,
      [newStatus, op.id]
    );
  });

  return sendJson(res, 200, { ok: true, message: `Operation ${ref} moved to ${action === "cancel" ? "Cancelled" : VALID_TRANSITIONS[body._prevStatus] || "next status"}.` });
});
