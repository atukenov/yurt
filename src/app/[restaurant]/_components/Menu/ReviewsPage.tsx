'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { FaStar, FaPlus } from 'react-icons/fa';
import { Spinner, Icon } from 'xtreme-ui';
import Button from '#components/base/Button';
import { toast } from 'react-toastify';

import { useRestaurant } from '#components/context/useContext';
import Modal from '#components/layout/Modal';
import NoContent from '#components/layout/NoContent';

import UserLogin from './UserLogin';
import './reviewsPage.scss';
import { IconContext } from 'react-icons';

type Review = {
	_id: string;
	customer: {
		fname: string;
		lname: string;
	};
	rating: number;
	comment: string;
	createdAt: string;
};

const ReviewsPage = () => {
	const session = useSession();
	const { restaurant, selectedAddress } = useRestaurant();
	const [reviews, setReviews] = useState<Review[]>([]);
	const [avgRating, setAvgRating] = useState(0);
	const [totalReviews, setTotalReviews] = useState(0);
	const [loading, setLoading] = useState(true);
	const [modalOpen, setModalOpen] = useState(false);
	const [loginModalOpen, setLoginModalOpen] = useState(false);
	const [submitting, setSubmitting] = useState(false);
	const [userReviewCount, setUserReviewCount] = useState(0);

	const [rating, setRating] = useState(0);
	const [hoveredRating, setHoveredRating] = useState(0);
	const [comment, setComment] = useState('');

	const isCustomer = session.status === 'authenticated' && session.data?.role === 'customer';
	const canAddReview = isCustomer && userReviewCount < 3;

	const handleStarClick = (starIndex: number, event: React.MouseEvent<HTMLDivElement>) => {
		const target = event.currentTarget;
		const rect = target.getBoundingClientRect();
		const clickX = event.clientX - rect.left;
		const starWidth = rect.width;
		const halfStar = clickX < starWidth / 2;
		
		setRating(halfStar ? starIndex - 0.5 : starIndex);
	};

	const handleStarHover = (starIndex: number, event: React.MouseEvent<HTMLDivElement>) => {
		const target = event.currentTarget;
		const rect = target.getBoundingClientRect();
		const hoverX = event.clientX - rect.left;
		const starWidth = rect.width;
		const halfStar = hoverX < starWidth / 2;
		
		setHoveredRating(halfStar ? starIndex - 0.5 : starIndex);
	};

	const renderStar = (starIndex: number, currentRating: number) => {
		const filled = currentRating >= starIndex;
		const halfFilled = currentRating >= starIndex - 0.5 && currentRating < starIndex;
		
		return (
			<div key={starIndex} className="starWrapper">
				<IconContext.Provider value={{ className: filled ? 'filled' : 'empty' }}>
					<FaStar />
				</IconContext.Provider>	
				{/* <FaStar color={filled ? '#ffc107' : '#e0e0e0'} className={filled ? 'filled' : 'empty'} /> */}
				{halfFilled && (
					<div className="halfStar">
						<FaStar color="#ffc107" className="filled" />
					</div>
				)}
			</div>
		);
	};

	useEffect(() => {
		if (restaurant?.username && selectedAddress) {
			fetchReviews();
		}
	}, [restaurant?.username, selectedAddress]);

	const fetchReviews = async () => {
		try {
			setLoading(true);
			const res = await fetch(
				`/api/reviews?restaurant=${restaurant?.username}&address=${encodeURIComponent(selectedAddress || '')}`
			);
			const data = await res.json();

			if (res.ok) {
				setReviews(data.reviews);
				setAvgRating(data.avgRating);
				setTotalReviews(data.totalReviews);

				// Count user's reviews
				if (isCustomer && session.data?.customer?._id) {
					const userReviews = data.reviews.filter(
						(review: any) => review.customer._id === session.data?.customer?._id
					);
					setUserReviewCount(userReviews.length);
				}
			}
		} catch (error) {
			console.error('Error fetching reviews:', error);
			toast.error('Failed to load reviews');
		} finally {
			setLoading(false);
		}
	};

	const handleSubmitReview = async () => {
		if (!rating) {
			return toast.error('Please select a rating');
		}

		if (!comment.trim()) {
			return toast.error('Please write a comment');
		}

		if (comment.length > 500) {
			return toast.error('Comment must be 500 characters or less');
		}

		try {
			setSubmitting(true);
			const res = await fetch('/api/reviews', {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify({
					restaurant: restaurant?.username,
					address: selectedAddress,
					rating,
					comment,
				}),
			});

			const data = await res.json();

			if (res.ok) {
				toast.success('Review submitted successfully!');
				setModalOpen(false);
				setRating(0);
				setComment('');
				fetchReviews();
			} else {
				toast.error(data.error || 'Failed to submit review');
			}
		} catch (error) {
			console.error('Error submitting review:', error);
			toast.error('Failed to submit review');
		} finally {
			setSubmitting(false);
		}
	};

	if (loading) {
		return (
			<div className="reviewsPage">
				<Spinner fullpage label="Loading reviews..." />
			</div>
		);
	}

	if (!selectedAddress) {
		return (
			<div className="reviewsPage">
				<NoContent
					label="Please select an address to view reviews"
					animationName="GhostNoContent"
				/>
			</div>
		);
	}

	return (
		<div className="reviewsPage">
			<div className="reviewsHeader">
				<div className="headerLeft">
					<h2>Customer Reviews</h2>
					<div className="ratingOverview">
						<div className="avgRating">
						<FaStar color="white" className="starIcon" />
							<span className="rating">{avgRating.toFixed(1)}</span>
						</div>
						<span className="totalReviews">({totalReviews} reviews)</span>
					</div>
				</div>
			{!isCustomer ? (
				<Button
					label="Log In to Review"
					icon="e323"
					onClick={() => setLoginModalOpen(true)}
				/>
			) : canAddReview ? (
				<Button
					label={`Add Review (${userReviewCount}/3)`}
					icon="2b"
					onClick={() => setModalOpen(true)}
				/>
			) : null}
				{reviews.length === 0 ? (
					<NoContent
						label="No reviews yet. Be the first to review!"
						animationName="GhostNoContent"
						size={200}
					/>
				) : (
					reviews.map((review) => (
						<div key={review._id} className="reviewCard">
							<div className="reviewHeader">
								<div className="topRow">
									<div className="customerInfo">
										<div className="avatar">
											{review.customer.fname[0]}
											{review.customer.lname[0]}
										</div>
										<div className="nameDate">
											<span className="name">
												{review.customer.fname} {review.customer.lname}
											</span>
											<span className="date">
												{new Date(review.createdAt).toLocaleDateString('en-US', {
													year: 'numeric',
													month: 'short',
													day: 'numeric',
												})}
											</span>
										</div>
									</div>
									<div className="ratingBadge">
										<FaStar color="white" />
										<span className="ratingValue">{review.rating.toFixed(1)}</span>
									</div>
								</div>
								<div className="stars">
									{[1, 2, 3, 4, 5].map((star) => {
										const filled = review.rating >= star;
										const halfFilled = review.rating >= star - 0.5 && review.rating < star;
										
										return (
											<div key={star} className="starWrapper">
												<FaStar color={filled ? '#ffc107' : '#e0e0e0'} className={filled ? 'filled' : 'empty'} />
												{halfFilled && (
													<div className="halfStar">
														<FaStar color="#ffc107" className="filled" />
													</div>
												)}
											</div>
										);
									})}
								</div>
							</div>
							<div className="reviewBody">
								<p>{review.comment}</p>
							</div>
						</div>
					))
				)}
			</div>

			<Modal open={modalOpen} setOpen={setModalOpen}>
				<div className="reviewModal">
					<h3>Write a Review</h3>
					<div className="modalBody">
						<div className="ratingInput">
							<label>Rating</label>
							<div className="starsInput">
								{[1, 2, 3, 4, 5].map((star) => (
									<div
										key={star}
										className="starContainer"
										onClick={(e) => handleStarClick(star, e)}
										onMouseMove={(e) => handleStarHover(star, e)}
										onMouseLeave={() => setHoveredRating(0)}
									>
										{renderStar(star, hoveredRating || rating)}
									</div>
								))}
							</div>
						</div>
						<div className="commentInput">
							<label>Your Review</label>
							<textarea
								placeholder="Share your experience... (max 500 characters)"
								value={comment}
								onChange={(e) => setComment(e.target.value)}
								maxLength={500}
							/>
							<span className="charCount">{comment.length}/500</span>
						</div>
						<div className="modalActions">
							<Button
								label="Cancel"
								type="secondary"
								onClick={() => setModalOpen(false)}
							/>
							<Button
								label="Submit Review"
								onClick={handleSubmitReview}
								loading={submitting}
							/>
						</div>
					</div>
				</div>
			</Modal>

			<Modal open={loginModalOpen} setOpen={setLoginModalOpen}>
				<UserLogin setOpen={setLoginModalOpen} />
			</Modal>
		</div>
	);
};

export default ReviewsPage;
