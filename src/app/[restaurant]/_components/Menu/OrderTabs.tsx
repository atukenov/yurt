import clsx from 'clsx';

import './orderTabs.scss';

const orderTabItems = [
	{ label: 'cart', route: 'cart' },
	{ label: 'active', route: 'active' },
	{ label: 'history', route: 'history' },
];

const OrderTabs = (props: TOrderTabsProps) => {
	const { currentTab, setCurrentTab } = props;

	return (
		<div className='orderTabs'>
			<div className='tabContainer'>
				{orderTabItems.map((item, i) => {
					return (
						<div
							key={i}
							className={clsx('tab', currentTab === item.route && 'active')}
							onClick={() => setCurrentTab(item.route)}
						>
							<span />
							<p>{item.label}</p>
						</div>
					);
				})}
			</div>
		</div>
	);
};

export default OrderTabs;

export type TOrderTabsProps = {
	currentTab: string;
	setCurrentTab: (tab: string) => void;
};
