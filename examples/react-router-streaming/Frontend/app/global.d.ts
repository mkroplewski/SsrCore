import "react-router";
import { greeterService } from "./_generated/api";

declare module "react-router" {
  interface AppLoadContext {
    greeterService: greeterService;
  }
}
