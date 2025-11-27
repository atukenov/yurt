import { useOrder } from '#components/context/useContext';
import ItemCard from '#components/layout/ItemCard';
import Lottie from '#components/base/Lottie';
import NoContent from '#components/layout/NoContent';
import { TMenu } from '#utils/database/models/menu';
import { getAnimSrc } from '#utils/constants/common';

import './customerActiveOrders.scss';

const CustomerActiveOrders = () => {
	const { order, cancelOrder, cancelingOrder } = useOrder();

	const approvedProducts = order?.products?.reduce(
		(acc, product) => (product.adminApproved ? acc + 1 : acc),
		0
	);

	const onCancelOrder = async () => {
		await cancelOrder();
	};

	if (!order?.products?.length) {
		return (
			<div className="customerActiveOrders">
				<NoContent
					label={"No active orders"}
					animationName="GhostNoContent"
				/>
			</div>
		);
	}

	if (approvedProducts === 0) {
		return (
			<div className="customerActiveOrders">
				<div className="orderApproval">
					<Lottie
						className="burgerLoader"
						src={getAnimSrc("FoodCook")}
					/>
					<div className="approvalHeading">
						<p>Your order</p>
						<p>will be accepted soon</p>
					</div>
					<button
						className="cancelOrder xButton secondaryDanger"
						disabled={cancelingOrder}
						onClick={onCancelOrder}
					>
						{cancelingOrder ? 'Canceling...' : 'Cancel Order'}
					</button>
				</div>
			</div>
		);
	}

	return (
		<div className="customerActiveOrders">
			<div className="orderItems">
				{order?.products.map((product, key) => {
					return (
						<ItemCard
							key={key}
							item={product as unknown as TMenuCustom}
							staticCard
						/>
					);
				})}
			</div>
			<div className="orderSummary">
				<div className="summaryRow">
					<span>Order Total</span>
					<span className="tenge">{order?.orderTotal}</span>
				</div>
				<div className="summaryRow">
					<span>Tax Total</span>
					<span className="tenge">{order?.taxTotal}</span>
				</div>
				<hr />
				<div className="summaryRow total">
					<span>Grand Total</span>
					<span className="tenge">{order?.orderTotal + order?.taxTotal}</span>
				</div>
			</div>
		</div>
	);
};

export default CustomerActiveOrders;

type TMenuCustom = TMenu & { quantity: number };
