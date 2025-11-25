'use client';

import { ReactNode, Suspense } from 'react';

import { SessionProvider } from 'next-auth/react';
import { XProvider } from 'xtreme-ui';

import { ToastManager } from '#components/base/ToastManager';

import { AddressProvider } from './Address';
import { AdminProvider } from './Admin';
import { OrderProvider } from './Order';
import { RestaurantProvider } from './Restaurant';

export const GlobalProvider = ({ children }: ProviderProps) => {
	return (
		<XProvider>
			<SessionProvider>
				<Suspense>
					{children}
				</Suspense>
			</SessionProvider>
		</XProvider>
	);
};

export const CustomerProvider = ({ children }: ProviderProps) => {
	return (
		<AddressProvider>
			<RestaurantProvider>
				<ToastManager />
				<OrderProvider>
					{children}
				</OrderProvider>
			</RestaurantProvider>
		</AddressProvider>
	);
};

export const DashboardProvider = ({ children }: ProviderProps) => {
	return (
		<AdminProvider>
			<ToastManager />
			{children}
		</AdminProvider>
	);
};
interface ProviderProps {
    children?: ReactNode
}
