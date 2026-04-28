import type { AdminSession, AdminTenantDirectoryDetailView } from "../../shared/types/admin-contracts";
import { Notice } from "../../shared/ui/Notice";
import { Panel } from "../../shared/ui/Panel";
import { useTenantManagementWorkspace } from "./model/useTenantManagementWorkspace";
import { TenantManagementApiClientsTab } from "./TenantManagementApiClientsTab";
import { TenantManagementOverviewTab } from "./TenantManagementOverviewTab";
import { TenantManagementReportsTab } from "./TenantManagementReportsTab";
import { TenantManagementRuntimeTab } from "./TenantManagementRuntimeTab";
import { TenantManagementUsersDevicesTab } from "./TenantManagementUsersDevicesTab";
import type { TenantManagementTab } from "./model/tenantManagementModel";
import styles from "./TenantManagementWorkspace.module.css";

interface TenantManagementWorkspaceProps {
  session: AdminSession;
  directory: AdminTenantDirectoryDetailView | null;
}

const tabs: { value: TenantManagementTab; label: string }[] = [
  { value: "overview", label: "Overview" },
  { value: "apiClients", label: "API clients" },
  { value: "usersDevices", label: "Users & devices" },
  { value: "runtime", label: "Runtime" },
  { value: "reports", label: "Reports" },
];

export function TenantManagementWorkspace({ session, directory }: TenantManagementWorkspaceProps) {
  if (!directory) {
    return (
      <Panel eyebrow="Tenant Management" title="Selected tenant operations">
        <p className={styles.empty}>Выберите tenant в directory, чтобы открыть tenant-scoped operations.</p>
      </Panel>
    );
  }

  return <TenantManagementWorkspaceContent session={session} directory={directory} />;
}

function TenantManagementWorkspaceContent(props: {
  session: AdminSession;
  directory: AdminTenantDirectoryDetailView;
}) {
  const { session, directory } = props;
  const workspace = useTenantManagementWorkspace(session, directory);

  return (
    <Panel eyebrow="Tenant Management" title={`${directory.tenant.displayName} operations`}>
      <div className={styles.layout}>
        {workspace.notice ? <Notice {...workspace.notice} /> : null}

        <div className={styles.tabList} role="tablist" aria-label="Tenant management sections">
          {tabs.map((tab) => (
            <button
              key={tab.value}
              type="button"
              role="tab"
              aria-selected={workspace.activeTab === tab.value}
              className={styles.tab}
              onClick={() => workspace.setActiveTab(tab.value)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {workspace.activeTab === "overview" ? (
          <TenantManagementOverviewTab directory={directory} />
        ) : null}
        {workspace.activeTab === "apiClients" ? (
          <TenantManagementApiClientsTab directory={directory} workspace={workspace} />
        ) : null}
        {workspace.activeTab === "usersDevices" ? (
          <TenantManagementUsersDevicesTab directory={directory} workspace={workspace} />
        ) : null}
        {workspace.activeTab === "runtime" ? (
          <TenantManagementRuntimeTab directory={directory} workspace={workspace} />
        ) : null}
        {workspace.activeTab === "reports" ? (
          <TenantManagementReportsTab workspace={workspace} />
        ) : null}
      </div>
    </Panel>
  );
}
