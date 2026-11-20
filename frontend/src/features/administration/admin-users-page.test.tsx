import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vite-plus/test";
import "../../app/locale-runtime";
import { AdminUsersPage } from "./admin-users-page";

const users = [
  {
    userId: "admin-1",
    email: "admin@example.test",
    displayName: "Admin",
    createdAt: "2026-11-20T10:00:00+00:00",
    emailConfirmed: true,
    isDemo: false,
    isAdmin: true,
  },
  {
    userId: "member-1",
    email: "member@example.test",
    displayName: "Member",
    createdAt: "2026-11-19T10:00:00+00:00",
    emailConfirmed: false,
    isDemo: false,
    isAdmin: false,
  },
];

function render(overrides: Partial<Parameters<typeof AdminUsersPage>[0]> = {}) {
  return renderToStaticMarkup(
    <AdminUsersPage
      signedIn
      isAdmin
      isLoading={false}
      users={users}
      currentUserId="admin-1"
      removingUserId={null}
      errorMessage={null}
      onRemove={() => {}}
      onSignIn={() => {}}
      {...overrides}
    />,
  );
}

describe("admin accounts page", () => {
  it("lists the accounts and protects the administrator's own row", () => {
    const markup = render();

    expect(markup).toContain("admin@example.test");
    expect(markup).toContain("member@example.test");
    expect(markup).toContain("Compte actuel");
    expect(markup).toContain("Admin");
    expect(markup).toContain("Retirer");
  });

  it("asks for a sign-in when signed out", () => {
    const markup = render({ signedIn: false, isAdmin: false, users: [] });

    expect(markup).toContain("Connecte-toi avec un compte administrateur");
  });

  it("refuses a signed-in account without the admin role", () => {
    const markup = render({ isAdmin: false, users: [] });

    expect(markup).toContain("réservée aux administrateurs");
  });

  it("shows the empty state", () => {
    const markup = render({ users: [] });

    expect(markup).toContain("Aucun compte enregistré.");
  });
});
