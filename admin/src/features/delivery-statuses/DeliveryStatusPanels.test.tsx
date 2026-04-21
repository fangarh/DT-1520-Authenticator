import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { AdminDeliveryChannel, AdminDeliveryStatus, AdminDeliveryStatusView } from "../../shared/types/admin-contracts";
import { DeliveryStatusDetailPanel } from "./DeliveryStatusDetailPanel";
import { DeliveryStatusListPanel } from "./DeliveryStatusListPanel";
import { DeliveryStatusLookupPanel } from "./DeliveryStatusLookupPanel";

function DeliveryStatusLookupHarness(props: { onSubmit: () => Promise<void> }) {
  const [tenantId, setTenantId] = useState("");
  const [applicationClientId, setApplicationClientId] = useState("");
  const [channel, setChannel] = useState<"" | AdminDeliveryChannel>("");
  const [status, setStatus] = useState<"" | AdminDeliveryStatus>("");
  const [limit, setLimit] = useState("25");

  return (
    <DeliveryStatusLookupPanel
      tenantId={tenantId}
      applicationClientId={applicationClientId}
      channel={channel}
      status={status}
      limit={limit}
      pending={false}
      canRead
      onTenantIdChange={setTenantId}
      onApplicationClientIdChange={setApplicationClientId}
      onChannelChange={(value) => setChannel(value as typeof channel)}
      onStatusChange={(value) => setStatus(value as typeof status)}
      onLimitChange={setLimit}
      onSubmit={props.onSubmit}
    />
  );
}

function DeliveryStatusSelectionHarness(props: { deliveries: AdminDeliveryStatusView[] }) {
  const [selectedDeliveryId, setSelectedDeliveryId] = useState<string | null>(null);
  const selectedDelivery = props.deliveries.find((delivery) => delivery.deliveryId === selectedDeliveryId) ?? null;

  return (
    <>
      <DeliveryStatusListPanel
        deliveries={props.deliveries}
        selectedDeliveryId={selectedDeliveryId}
        onSelect={(delivery) => setSelectedDeliveryId(delivery.deliveryId)}
      />
      <DeliveryStatusDetailPanel delivery={selectedDelivery} />
    </>
  );
}

describe("Delivery status panels", () => {
  it("propagates filter changes and submit", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<DeliveryStatusLookupHarness onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Tenant ID"), "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    await user.type(screen.getByLabelText("Application Client Filter"), "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    await user.selectOptions(screen.getByLabelText("Channel"), "webhook_event");
    await user.selectOptions(screen.getByLabelText("Status"), "failed");
    await user.clear(screen.getByLabelText("Limit"));
    await user.type(screen.getByLabelText("Limit"), "10");
    await user.click(screen.getByRole("button", { name: "Load deliveries" }));

    expect((screen.getByLabelText("Tenant ID") as HTMLInputElement).value).toBe("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    expect((screen.getByLabelText("Application Client Filter") as HTMLInputElement).value).toBe("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    expect((screen.getByLabelText("Channel") as HTMLSelectElement).value).toBe("webhook_event");
    expect((screen.getByLabelText("Status") as HTMLSelectElement).value).toBe("failed");
    expect((screen.getByLabelText("Limit") as HTMLInputElement).value).toBe("10");
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("shows selected delivery details from the inventory list", async () => {
    const user = userEvent.setup();
    const delivery: AdminDeliveryStatusView = {
      deliveryId: "delivery-1",
      tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
      applicationClientId: "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4",
      channel: "webhook_event",
      status: "failed",
      eventType: "device.blocked",
      deliveryDestination: "https://crm.example.com/webhooks/platform",
      subjectType: "device",
      subjectId: "device-1",
      publicationId: "publication-1",
      attemptCount: 3,
      occurredAtUtc: "2026-04-20T10:00:00Z",
      createdAtUtc: "2026-04-20T10:00:10Z",
      nextAttemptAtUtc: "2026-04-20T10:05:00Z",
      lastAttemptAtUtc: "2026-04-20T10:04:00Z",
      deliveredAtUtc: null,
      lastErrorCode: "delivery_failed",
      isRetryScheduled: true,
    };

    render(<DeliveryStatusSelectionHarness deliveries={[delivery]} />);

    await user.click(screen.getByRole("button", { name: "Inspect" }));

    expect(screen.getAllByText("device.blocked").length).toBeGreaterThan(0);
    expect(screen.getAllByText("https://crm.example.com/webhooks/platform").length).toBeGreaterThan(0);
    expect(screen.getAllByText("retry scheduled").length).toBeGreaterThan(0);
    expect(screen.getByText("delivery_failed")).toBeTruthy();
    expect(screen.getByText("2026-04-20 10:05:00 UTC")).toBeTruthy();
  });
});
