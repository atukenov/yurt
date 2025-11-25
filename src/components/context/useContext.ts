import { useContext } from "react";

import { AddressContext } from "./Address";
import { AdminContext } from "./Admin";
import { OrderContext } from "./Order";
import { RestaurantContext } from "./Restaurant";

export const useRestaurant = () => useContext(RestaurantContext);
export const useOrder = () => useContext(OrderContext);
export const useAdmin = () => useContext(AdminContext);
export const useAddress = () => useContext(AddressContext);
