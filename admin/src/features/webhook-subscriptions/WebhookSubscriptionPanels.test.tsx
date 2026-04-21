import { render, screen } from "@testing-library/react";
import { useState } from "react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import type { WebhookEventType, WebhookSubscriptionView } from "../../shared/types/admin-contracts";
import { WebhookSubscriptionEditorPanel } from "./WebhookSubscriptionEditorPanel";
import { WebhookSubscriptionListPanel } from "./WebhookSubscriptionListPanel";

function WebhookSubscriptionEditorHarness(props: { onSubmit: () => Promise<void> }) {
  const [applicationClientId, setApplicationClientId] = useState("");
  const [endpointUrl, setEndpointUrl] = useState("");
  const [eventTypes, setEventTypes] = useState<WebhookEventType[]>(["challenge.approved"]);
  const [isActive, setIsActive] = useState(true);

  function toggleEventType(eventType: WebhookEventType) {
    setEventTypes((current) => (
      current.includes(eventType)
        ? current.filter((item) => item !== eventType)
        : [...current, eventType]
    ));
  }

  return (
    <WebhookSubscriptionEditorPanel
      applicationClientId={applicationClientId}
      endpointUrl={endpointUrl}
      eventTypes={eventTypes}
      isActive={isActive}
      pending={false}
      canWrite
      onApplicationClientIdChange={setApplicationClientId}
      onEndpointUrlChange={setEndpointUrl}
      onToggleEventType={toggleEventType}
      onIsActiveChange={setIsActive}
      onSubmit={props.onSubmit}
      onReset={vi.fn()}
    />
  );
}

describe("Webhook subscription panels", () => {
  it("propagates editor changes and submit", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(<WebhookSubscriptionEditorHarness onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Application Client ID"), "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    await user.type(screen.getByLabelText("Webhook endpoint URL"), "https://crm.example.com/webhooks/platform");
    await user.click(screen.getByLabelText("Subscription active"));
    await user.click(screen.getByLabelText(/device\.activated/i));
    await user.click(screen.getByRole("button", { name: "Save subscription" }));

    expect((screen.getByLabelText("Application Client ID") as HTMLInputElement).value).toBe("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");
    expect((screen.getByLabelText("Webhook endpoint URL") as HTMLInputElement).value).toBe("https://crm.example.com/webhooks/platform");
    expect((screen.getByLabelText("Subscription active") as HTMLInputElement).checked).toBe(false);
    expect((screen.getByLabelText(/device\.activated/i) as HTMLInputElement).checked).toBe(true);
    expect(onSubmit).toHaveBeenCalledOnce();
  });

  it("forwards selected subscription from the inventory list", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    const subscription: WebhookSubscriptionView = {
      subscriptionId: "webhook-subscription-1",
      tenantId: "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb",
      applicationClientId: "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4",
      endpointUrl: "https://crm.example.com/webhooks/platform",
      status: "active",
      eventTypes: ["challenge.approved", "device.activated"],
      createdUtc: "2026-04-20T10:00:00.000Z",
      updatedUtc: "2026-04-20T10:10:00.000Z",
    };

    render(
      <WebhookSubscriptionListPanel
        subscriptions={[subscription]}
        selectedSubscriptionId={null}
        onSelect={onSelect}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Edit subscription" }));

    expect(screen.getByText("https://crm.example.com/webhooks/platform")).toBeTruthy();
    expect(onSelect).toHaveBeenCalledWith(subscription);
  });
});
