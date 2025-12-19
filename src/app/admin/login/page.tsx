import { DashboardProvider } from "#components/context";

import AdminLoginPageContainer from "./AdminLoginPageContainer";

export default async function AdminLoginPage() {
  return (
    <DashboardProvider>
      <AdminLoginPageContainer />
    </DashboardProvider>
  );
}
