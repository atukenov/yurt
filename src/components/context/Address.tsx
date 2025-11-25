import { createContext, ReactNode, useEffect, useState } from 'react';

const AddressDefault: TAddressInitialType = {
	selectedAddress: null,
	setSelectedAddress: () => {},
};

export const AddressContext = createContext(AddressDefault);

export const AddressProvider = ({ children }: TAddressProviderProps) => {
	const [selectedAddress, setSelectedAddressState] = useState<string | null>(null);

	// Load from sessionStorage on mount
	useEffect(() => {
		const stored = sessionStorage.getItem('selectedAddress');
		if (stored) {
			setSelectedAddressState(stored);
		}
	}, []);

	// Persist to sessionStorage when it changes
	const setSelectedAddress = (address: string | null) => {
		setSelectedAddressState(address);
		if (address) {
			sessionStorage.setItem('selectedAddress', address);
		} else {
			sessionStorage.removeItem('selectedAddress');
		}
	};

	return (
		<AddressContext.Provider value={{ selectedAddress, setSelectedAddress }}>
			{children}
		</AddressContext.Provider>
	);
};

export type TAddressProviderProps = {
    children?: ReactNode
}

export type TAddressInitialType = {
	selectedAddress: string | null;
	setSelectedAddress: (address: string | null) => void;
}
