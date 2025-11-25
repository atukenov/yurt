import { createContext, ReactNode, useEffect, useState } from 'react';

import { usePathname, useSearchParams } from 'next/navigation';
import useSWR from 'swr';

import { TAccount } from '#utils/database/models/account';
import { fetcher } from '#utils/helper/common';

const RestaurantDefault: TRestaurantInitialType = {
	restaurant: undefined,
	error: undefined,
	loading: false,
	selectedAddress: null,
};

export const RestaurantContext = createContext(RestaurantDefault);
export const RestaurantProvider = ({ children }: TRestaurantProviderProps) => {
	const pathname = usePathname();
	const searchParams = useSearchParams();
	const addressFromQuery = searchParams.get('address');
	const [selectedAddress, setSelectedAddress] = useState<string | null>(null);
	
	const { data: restaurant, error, isLoading } = useSWR(`/api/menu?id=${pathname.replace('/', '')}`, fetcher);

	// Load address from query params or sessionStorage
	useEffect(() => {
		if (addressFromQuery) {
			setSelectedAddress(addressFromQuery);
			sessionStorage.setItem('selectedAddress', addressFromQuery);
		} else {
			const stored = sessionStorage.getItem('selectedAddress');
			if (stored) {
				setSelectedAddress(stored);
			}
		}
	}, [addressFromQuery]);

	return (
		<RestaurantContext.Provider value={{ restaurant, error, loading: isLoading, selectedAddress }}>
			{children}
		</RestaurantContext.Provider>
	);
};

export type TRestaurantProviderProps = {
    children?: ReactNode
}

export type TRestaurantInitialType = {
	restaurant?: TAccount,
	error: unknown,
	loading: boolean,
	selectedAddress: string | null,
}
