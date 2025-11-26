import clsx from 'clsx';
import { useSearchParams } from 'next/navigation';
import { Button } from 'xtreme-ui';

import { TOrder } from '#utils/database/models/order';

import './ordersCard.scss';

const OrdersCard = (props: TOrdersCard) => {
	const { data, actions, active, reject, setReject, busy, history, details, action, activate, showDetails } = props;
	const queryParams = useSearchParams();
	const subTab = queryParams.get('subTab') ?? '';

	const address = data.address;
	const customerName = `${data?.customer?.fname} ${data?.customer?.lname}`;

	const OptionButtons = () => {
		if (!actions) return null;

		if (subTab === 'active') {
			return (
				<div className='options'>
					<Button
						className='accept'
						size='mini'
						icon='f00c'
						label={!props.reject ? 'Complete' : 'Yes do it!'}
						onClick={() => action?.(data._id.toString())}
						loading={busy}
					/>
					{
						!busy &&
						<Button className='reject' size='mini'
							type='primaryDanger' icon='f00d'
							label={!reject ? 'Cancel' : 'No Don\'t'}
							onClick={() => {
								setReject?.({
									_id: !reject ? data._id.toString() : null,
									details: false,
								});
							}}
						/>
					}
				</div>
			);
		}
		return (
			<div className='options'>
				<Button className='accept' label={!reject ? 'Accept' : 'Yes do it!'}
					size='mini'
					icon='f00c'
					onClick={() => action?.(data._id.toString())} loading={busy}
				/>
				{
					!busy &&
					<Button
						className='reject'
						size='mini'
						type='primaryDanger' icon='f00d'
						onClick={() => {
							setReject?.({
								_id: !reject ? data._id.toString() : null,
								details: false,
							});
						}} label={!reject ? 'Reject' : 'No Don\'t'}
					/>
				}
			</div>
		);
	};

	const classList = clsx(
		'ordersCard',
		active && 'active',
		reject && 'reject',
		busy && 'busy',
	);

	return (
		<div className={classList}
			onClick={() => {
				!active && !history && setReject?.({ _id: null, details: false });
				activate(data._id.toString());
			}}
		>
			<div className='content'>
				<p className='table'>{!reject || details ? `Address: ${address}` : 'Are you sure?'}</p>
				<p className='name'>{!reject || details ? customerName : `Address: ${address}`}</p>
				{
					!data?.products?.length
						? <p className='noContent'>No orders yet</p>
						: <p className='total tenge' onClick={() => showDetails?.(true)}>{data?.orderTotal}</p>
				}
				<OptionButtons />
			</div>
		</div>
	);
};

export default OrdersCard;

type TOrdersCard = {
	data: TOrder
	actions?: boolean
	history?: boolean
	active?: boolean
	reject?: boolean
	setReject?: (props: { _id: string | null, details: boolean }) => void
	busy?: boolean
	details?: boolean
	action?: (id: string) => void
	showDetails?: (value: boolean) => void
	activate: (id: string) => void
}
