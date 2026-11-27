import { RouterProvider } from "@tanstack/react-router";
import { AppQueryProvider } from "./app/query-provider";
import { router } from "./app/router";

function App() {
  return (
    <AppQueryProvider>
      <RouterProvider router={router} />
    </AppQueryProvider>
  );
}

export default App;
